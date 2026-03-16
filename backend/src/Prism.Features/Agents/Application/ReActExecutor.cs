using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.Agents.Domain;
using Prism.Features.Agents.Domain.Tools;

namespace Prism.Features.Agents.Application;

/// <summary>
/// Executes the ReAct (Reasoning-Action-Observation) agent pattern.
/// Iteratively prompts the model for thoughts and actions, executes tools,
/// and feeds observations back until a final answer or stop condition is met.
/// </summary>
public sealed class ReActExecutor
{
    private readonly ILogger<ReActExecutor> _logger;

    private static readonly Regex ThoughtRegex = new(@"Thought:\s*(.+?)(?=\n(?:Action|Final Answer):|\z)", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ActionRegex = new(@"Action:\s*(.+?)(?:\n|$)", RegexOptions.Compiled);
    private static readonly Regex ActionInputRegex = new(@"Action Input:\s*(.+?)(?=\n(?:Thought|Action|Final Answer):|\z)", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex FinalAnswerRegex = new(@"Final Answer:\s*(.+)", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReActExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ReActExecutor(ILogger<ReActExecutor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds the ReAct system prompt with available tool descriptions.
    /// </summary>
    /// <param name="baseSystemPrompt">The workflow's base system prompt.</param>
    /// <param name="tools">The available tools for this execution.</param>
    /// <returns>The complete system prompt including ReAct instructions.</returns>
    public static string BuildSystemPrompt(string baseSystemPrompt, IReadOnlyList<IAgentTool> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine(baseSystemPrompt);
        sb.AppendLine();
        sb.AppendLine("You are an AI agent that uses the ReAct (Reasoning and Acting) framework.");
        sb.AppendLine("For each step, you MUST respond in EXACTLY this format:");
        sb.AppendLine();
        sb.AppendLine("Thought: <your reasoning about what to do next>");
        sb.AppendLine("Action: <tool_name>");
        sb.AppendLine("Action Input: <input to the tool>");
        sb.AppendLine();
        sb.AppendLine("After receiving an observation, continue with another Thought/Action/Action Input cycle.");
        sb.AppendLine("When you have enough information to answer the original question, respond with:");
        sb.AppendLine();
        sb.AppendLine("Thought: <your final reasoning>");
        sb.AppendLine("Final Answer: <your complete answer>");
        sb.AppendLine();
        sb.AppendLine("Available tools:");

        foreach (IAgentTool tool in tools)
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Executes the ReAct loop, yielding steps as they occur for real-time streaming.
    /// </summary>
    /// <param name="provider">The inference provider to use for model calls.</param>
    /// <param name="workflow">The agent workflow configuration.</param>
    /// <param name="tools">The available tools.</param>
    /// <param name="userInput">The user's input query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An async enumerable of agent steps.</returns>
    public async IAsyncEnumerable<AgentStep> ExecuteAsync(
        IInferenceProvider provider,
        AgentWorkflow workflow,
        IReadOnlyList<IAgentTool> tools,
        string userInput,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string systemPrompt = BuildSystemPrompt(workflow.SystemPrompt, tools);
        var messages = new List<ChatMessage>
        {
            ChatMessage.System(systemPrompt),
            new(ChatMessage.UserRole, userInput)
        };

        int totalTokens = 0;
        AgentToolRegistry registry = new();
        foreach (IAgentTool tool in tools)
        {
            registry.Register(tool);
        }

        for (int step = 0; step < workflow.MaxSteps; step++)
        {
            if (totalTokens >= workflow.TokenBudget)
            {
                _logger.LogWarning("Agent token budget {Budget} exceeded at step {Step} with {Tokens} tokens",
                    workflow.TokenBudget, step, totalTokens);

                yield return new AgentStep
                {
                    Index = step,
                    Thought = "Token budget exceeded. Stopping execution.",
                    IsFinalAnswer = true,
                    FinalAnswer = "I ran out of my token budget before completing the task. Here is what I found so far based on my reasoning.",
                    Error = "Token budget exceeded"
                };
                yield break;
            }

            var chatRequest = new ChatRequest
            {
                Model = workflow.Model,
                Messages = messages,
                Temperature = workflow.Temperature,
                MaxTokens = Math.Min(1024, workflow.TokenBudget - totalTokens),
                SourceModule = "agents"
            };

            Stopwatch stepWatch = Stopwatch.StartNew();
            Common.Results.Result<ChatResponse> result = await provider.ChatAsync(chatRequest, ct);
            stepWatch.Stop();

            if (result.IsFailure)
            {
                yield return new AgentStep
                {
                    Index = step,
                    Error = $"Inference failed: {result.Error.Message}",
                    LatencyMs = stepWatch.ElapsedMilliseconds
                };
                yield break;
            }

            ChatResponse response = result.Value;
            string content = response.Content ?? "";
            int stepTokens = (response.Usage?.TotalTokens ?? 0);
            totalTokens += stepTokens;

            // Parse the model's response
            AgentStep agentStep = ParseResponse(content, step);
            agentStep.TokensUsed = stepTokens;
            agentStep.LatencyMs = stepWatch.ElapsedMilliseconds;

            // Add the assistant message to conversation
            messages.Add(new ChatMessage(ChatMessage.AssistantRole, content));

            // Check for final answer
            if (agentStep.IsFinalAnswer)
            {
                _logger.LogInformation("Agent reached final answer at step {Step} with {TotalTokens} total tokens",
                    step, totalTokens);
                yield return agentStep;
                yield break;
            }

            // Execute the tool if an action was specified
            if (!string.IsNullOrEmpty(agentStep.Action))
            {
                IAgentTool? tool = registry.Get(agentStep.Action);
                if (tool is null)
                {
                    agentStep.Observation = $"Error: Unknown tool '{agentStep.Action}'. Available tools: {string.Join(", ", tools.Select(t => t.Name))}";
                }
                else
                {
                    try
                    {
                        ToolResult toolResult = await tool.ExecuteAsync(agentStep.ActionInput ?? "", ct);
                        agentStep.Observation = toolResult.Success
                            ? toolResult.Output
                            : $"Error: {toolResult.Error}";
                    }
                    catch (Exception ex)
                    {
                        agentStep.Observation = $"Tool execution error: {ex.Message}";
                        _logger.LogError(ex, "Tool {ToolName} failed at step {Step}", agentStep.Action, step);
                    }
                }

                // Add the observation to the conversation
                messages.Add(new ChatMessage(ChatMessage.UserRole, $"Observation: {agentStep.Observation}"));
            }

            yield return agentStep;
        }

        // Max steps reached
        _logger.LogWarning("Agent reached max steps {MaxSteps} without final answer", workflow.MaxSteps);
        yield return new AgentStep
        {
            Index = workflow.MaxSteps,
            Thought = "Maximum steps reached without finding a final answer.",
            IsFinalAnswer = true,
            FinalAnswer = "I reached the maximum number of steps. Please try with a higher step limit or a more specific question.",
            Error = "Max steps exceeded"
        };
    }

    private static AgentStep ParseResponse(string content, int index)
    {
        var step = new AgentStep { Index = index };

        // Try to extract Final Answer first
        Match finalMatch = FinalAnswerRegex.Match(content);
        if (finalMatch.Success)
        {
            step.IsFinalAnswer = true;
            step.FinalAnswer = finalMatch.Groups[1].Value.Trim();

            // Also extract the thought if present
            Match thoughtMatch = ThoughtRegex.Match(content);
            if (thoughtMatch.Success)
            {
                step.Thought = thoughtMatch.Groups[1].Value.Trim();
            }

            return step;
        }

        // Extract Thought
        Match thought = ThoughtRegex.Match(content);
        if (thought.Success)
        {
            step.Thought = thought.Groups[1].Value.Trim();
        }

        // Extract Action
        Match action = ActionRegex.Match(content);
        if (action.Success)
        {
            step.Action = action.Groups[1].Value.Trim();
        }

        // Extract Action Input
        Match actionInput = ActionInputRegex.Match(content);
        if (actionInput.Success)
        {
            step.ActionInput = actionInput.Groups[1].Value.Trim();
        }

        // If we couldn't parse anything meaningful, treat the whole response as a thought
        if (step.Thought is null && step.Action is null && step.FinalAnswer is null)
        {
            step.Thought = content.Trim();
        }

        return step;
    }
}

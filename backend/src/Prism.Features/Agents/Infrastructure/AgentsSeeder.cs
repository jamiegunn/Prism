using Microsoft.EntityFrameworkCore;
using Prism.Common.Database.Seeders;
using Prism.Features.Agents.Domain;
using Prism.Features.Models.Infrastructure;

namespace Prism.Features.Agents.Infrastructure;

/// <summary>
/// Seeds a sample agent workflow and a completed run to demonstrate
/// the Agent Builder feature on first launch.
/// </summary>
public sealed class AgentsSeeder : IDataSeeder
{
    /// <summary>
    /// Gets the execution order. Agents seed at order 160, after RAG.
    /// </summary>
    public int Order => 160;

    /// <summary>
    /// Seeds a sample agent workflow with one completed run if none exist.
    /// Creates a research assistant workflow using the ReAct pattern with a three-step execution trace.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasWorkflows = await context.Set<AgentWorkflow>().AnyAsync(ct);

        if (hasWorkflows)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        Guid workflowId = Guid.NewGuid();

        List<AgentStep> steps =
        [
            new AgentStep
            {
                Index = 0,
                Thought = "I should search the knowledge base for information about the Transformer architecture.",
                Action = "rag_query",
                ActionInput = "Transformer architecture contributions",
                Observation = "The Transformer architecture, introduced in 'Attention Is All You Need' by Vaswani et al. (2017), " +
                              "revolutionized natural language processing by replacing recurrent neural networks with a purely " +
                              "attention-based mechanism...",
                IsFinalAnswer = false,
                TokensUsed = 120,
                LatencyMs = 1100
            },
            new AgentStep
            {
                Index = 1,
                Thought = "I have good information about the Transformer. Let me search for more details about its specific contributions.",
                Action = "rag_query",
                ActionInput = "Transformer training parallelization BLEU score results",
                Observation = "The Transformer achieved state-of-the-art results on machine translation benchmarks, " +
                              "reaching 28.4 BLEU on the WMT 2014 English-to-German translation task. Training is " +
                              "significantly more parallelizable than previous sequence-to-sequence models...",
                IsFinalAnswer = false,
                TokensUsed = 150,
                LatencyMs = 1050
            },
            new AgentStep
            {
                Index = 2,
                Thought = "I now have enough information to provide a comprehensive answer about the key contributions.",
                IsFinalAnswer = true,
                FinalAnswer = "Based on my research, the key contributions of the Transformer architecture are:\n\n" +
                              "1. **Self-attention mechanism** — replacing recurrence with attention allows the model to weigh " +
                              "the importance of different parts of the input when producing each output element.\n\n" +
                              "2. **Multi-head attention** — allows the model to jointly attend to information from different " +
                              "representation subspaces, capturing richer relationships.\n\n" +
                              "3. **Parallelizable training** — unlike sequential RNNs, the Transformer can process all positions " +
                              "simultaneously, significantly reducing training time.\n\n" +
                              "4. **State-of-the-art results** — achieved 28.4 BLEU on the WMT 2014 English-to-German " +
                              "translation task, surpassing previous approaches.",
                TokensUsed = 180,
                LatencyMs = 1050
            }
        ];

        var workflow = new AgentWorkflow
        {
            Id = workflowId,
            Name = "Research Assistant",
            Description = "An agent that helps analyze research topics using available tools",
            SystemPrompt = "You are a research assistant. Use available tools to gather information and provide " +
                           "well-sourced answers. Think step-by-step.",
            Model = "meta-llama/Llama-3.1-8B-Instruct",
            InstanceId = ModelsSeeder.VllmSeedInstanceId,
            Pattern = AgentPatternType.ReAct,
            MaxSteps = 10,
            TokenBudget = 8000,
            Temperature = 0.3,
            EnabledTools = ["calculator", "echo", "rag_query"],
            Version = 1,
            Runs =
            [
                new AgentRun
                {
                    WorkflowId = workflowId,
                    Status = AgentRunStatus.Completed,
                    Input = "What are the key contributions of the Transformer architecture?",
                    Output = "Based on my research, the key contributions of the Transformer architecture are:\n\n" +
                             "1. **Self-attention mechanism** — replacing recurrence with attention allows the model to weigh " +
                             "the importance of different parts of the input when producing each output element.\n\n" +
                             "2. **Multi-head attention** — allows the model to jointly attend to information from different " +
                             "representation subspaces, capturing richer relationships.\n\n" +
                             "3. **Parallelizable training** — unlike sequential RNNs, the Transformer can process all positions " +
                             "simultaneously, significantly reducing training time.\n\n" +
                             "4. **State-of-the-art results** — achieved 28.4 BLEU on the WMT 2014 English-to-German " +
                             "translation task, surpassing previous approaches.",
                    StepsJson = JsonSerializer.Serialize(steps),
                    StepCount = 3,
                    TotalTokens = 450,
                    TotalLatencyMs = 3200,
                    StartedAt = now.AddMilliseconds(-3200),
                    CompletedAt = now
                }
            ]
        };

        context.Set<AgentWorkflow>().Add(workflow);
        await context.SaveChangesAsync(ct);
    }
}

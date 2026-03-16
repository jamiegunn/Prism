using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Domain;
using Prism.Features.Agents.Domain.Tools;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;

namespace Prism.Features.Agents.Application.RunAgent;

/// <summary>
/// Command to execute an agent workflow with a user input.
/// </summary>
public sealed record RunAgentCommand(Guid WorkflowId, string Input);

/// <summary>
/// SSE event emitted during agent execution.
/// </summary>
public abstract record AgentRunEvent;

/// <summary>
/// Emitted when the agent run starts.
/// </summary>
public sealed record AgentRunStarted(Guid RunId, Guid WorkflowId) : AgentRunEvent;

/// <summary>
/// Emitted when a step completes during agent execution.
/// </summary>
public sealed record AgentStepCompleted(AgentStep Step) : AgentRunEvent;

/// <summary>
/// Emitted when the agent run finishes.
/// </summary>
public sealed record AgentRunFinished(AgentRunDto Run) : AgentRunEvent;

/// <summary>
/// Emitted when an error occurs during agent execution.
/// </summary>
public sealed record AgentRunError(string Error) : AgentRunEvent;

/// <summary>
/// Handles execution of agent workflows using the ReAct pattern with SSE streaming.
/// </summary>
public sealed class RunAgentHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ReActExecutor _executor;
    private readonly AgentToolRegistry _toolRegistry;
    private readonly ILogger<RunAgentHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunAgentHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="providerFactory">The inference provider factory.</param>
    /// <param name="executor">The ReAct executor.</param>
    /// <param name="toolRegistry">The agent tool registry.</param>
    /// <param name="logger">The logger instance.</param>
    public RunAgentHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ReActExecutor executor,
        AgentToolRegistry toolRegistry,
        ILogger<RunAgentHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _executor = executor;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Executes an agent workflow, streaming steps as SSE events.
    /// </summary>
    /// <param name="command">The run agent command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An async enumerable of agent run events.</returns>
    public async IAsyncEnumerable<AgentRunEvent> HandleAsync(
        RunAgentCommand command,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Load workflow
        AgentWorkflow? workflow = await _db.Set<AgentWorkflow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == command.WorkflowId, ct);

        if (workflow is null)
        {
            yield return new AgentRunError($"Agent workflow {command.WorkflowId} not found.");
            yield break;
        }

        // Find inference instance
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == workflow.InstanceId, ct);

        if (instance is null)
        {
            yield return new AgentRunError($"Inference instance {workflow.InstanceId} not found.");
            yield break;
        }

        // Create the run record
        var run = new AgentRun
        {
            WorkflowId = workflow.Id,
            Status = AgentRunStatus.Running,
            Input = command.Input,
            StartedAt = DateTime.UtcNow
        };

        _db.Set<AgentRun>().Add(run);
        await _db.SaveChangesAsync(ct);

        yield return new AgentRunStarted(run.Id, workflow.Id);

        // Resolve tools
        IReadOnlyList<IAgentTool> tools = _toolRegistry.GetByNames(workflow.EnabledTools);

        // Create provider
        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        var steps = new List<AgentStep>();
        Stopwatch totalWatch = Stopwatch.StartNew();
        string? finalAnswer = null;
        string? errorMessage = null;

        // Use enumerator pattern to avoid yield inside try/catch (C# limitation)
        IAsyncEnumerator<AgentStep>? enumerator = null;
        string? initError = null;

        try
        {
            enumerator = _executor.ExecuteAsync(provider, workflow, tools, command.Input, ct).GetAsyncEnumerator(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize agent execution for run {RunId}", run.Id);
            initError = ex.Message;
        }

        if (initError is not null)
        {
            run.Status = AgentRunStatus.Failed;
            errorMessage = initError;
            yield return new AgentRunError(initError);
        }
        else
        {
            bool hasMore = true;
            while (hasMore)
            {
                AgentStep? currentStep = null;
                string? stepError = null;

                try
                {
                    hasMore = await enumerator!.MoveNextAsync();
                    if (hasMore)
                    {
                        currentStep = enumerator.Current;
                    }
                }
                catch (OperationCanceledException)
                {
                    run.Status = AgentRunStatus.Cancelled;
                    errorMessage = "Run was cancelled.";
                    hasMore = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Agent run {RunId} failed at step {StepIndex}", run.Id, steps.Count);
                    run.Status = AgentRunStatus.Failed;
                    errorMessage = ex.Message;
                    stepError = ex.Message;
                    hasMore = false;
                }

                if (currentStep is not null)
                {
                    steps.Add(currentStep);
                    yield return new AgentStepCompleted(currentStep);

                    if (currentStep.IsFinalAnswer && currentStep.FinalAnswer is not null)
                    {
                        finalAnswer = currentStep.FinalAnswer;
                    }

                    if (currentStep.Error is not null && currentStep.IsFinalAnswer)
                    {
                        errorMessage = currentStep.Error;
                    }
                }

                if (stepError is not null)
                {
                    yield return new AgentRunError(stepError);
                }
            }

            if (enumerator is not null)
            {
                await enumerator.DisposeAsync();
            }
        }

        totalWatch.Stop();

        // Update run record
        run.StepsJson = JsonSerializer.Serialize(steps);
        run.StepCount = steps.Count;
        run.TotalTokens = steps.Sum(s => s.TokensUsed);
        run.TotalLatencyMs = totalWatch.ElapsedMilliseconds;
        run.Output = finalAnswer;
        run.ErrorMessage = errorMessage;
        run.CompletedAt = DateTime.UtcNow;

        if (run.Status == AgentRunStatus.Running)
        {
            run.Status = errorMessage is not null && finalAnswer is null
                ? AgentRunStatus.Failed
                : AgentRunStatus.Completed;
        }

        _db.Set<AgentRun>().Update(run);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Agent run {RunId} completed with status {Status} in {Steps} steps using {Tokens} tokens",
            run.Id, run.Status, run.StepCount, run.TotalTokens);

        yield return new AgentRunFinished(AgentRunDto.FromEntity(run));
    }
}

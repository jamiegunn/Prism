using Prism.Common.Database;

namespace Prism.Features.Agents.Domain;

/// <summary>
/// Represents a single execution of an agent workflow, tracking all steps,
/// token usage, and the final result.
/// </summary>
public sealed class AgentRun : BaseEntity
{
    /// <summary>
    /// Gets or sets the ID of the workflow this run belongs to.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the current execution status of this run.
    /// </summary>
    public AgentRunStatus Status { get; set; } = AgentRunStatus.Pending;

    /// <summary>
    /// Gets or sets the user input that initiated this run.
    /// </summary>
    public string Input { get; set; } = "";

    /// <summary>
    /// Gets or sets the final output produced by the agent.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Gets or sets the error message if the run failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the list of execution steps as a JSON-serialized collection.
    /// </summary>
    public string StepsJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the total number of steps executed.
    /// </summary>
    public int StepCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of tokens consumed across all steps.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the total latency in milliseconds for the entire run.
    /// </summary>
    public long TotalLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the run started executing.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the run finished (completed, failed, or cancelled).
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

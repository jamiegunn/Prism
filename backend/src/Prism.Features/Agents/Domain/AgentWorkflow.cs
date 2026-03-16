using Prism.Common.Database;

namespace Prism.Features.Agents.Domain;

/// <summary>
/// Represents a configurable agent workflow that defines how an AI agent
/// reasons, selects tools, and executes multi-step tasks.
/// </summary>
public sealed class AgentWorkflow : BaseEntity
{
    /// <summary>
    /// Gets or sets the optional project this workflow belongs to.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the display name for this workflow.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the description of what this workflow does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the system prompt that instructs the agent on its role and behavior.
    /// </summary>
    public string SystemPrompt { get; set; } = "";

    /// <summary>
    /// Gets or sets the model identifier to use for inference.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// Gets or sets the inference instance ID to use for this workflow.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the execution pattern (ReAct, Sequential).
    /// </summary>
    public AgentPatternType Pattern { get; set; } = AgentPatternType.ReAct;

    /// <summary>
    /// Gets or sets the maximum number of reasoning steps before the agent is forced to stop.
    /// </summary>
    public int MaxSteps { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum total token budget for a single run.
    /// </summary>
    public int TokenBudget { get; set; } = 8000;

    /// <summary>
    /// Gets or sets the sampling temperature for the agent's inference calls.
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the list of tool names enabled for this workflow.
    /// </summary>
    public List<string> EnabledTools { get; set; } = [];

    /// <summary>
    /// Gets or sets the version number of this workflow configuration.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the collection of runs executed with this workflow.
    /// </summary>
    public List<AgentRun> Runs { get; set; } = [];
}

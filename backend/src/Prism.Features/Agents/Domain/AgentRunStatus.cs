namespace Prism.Features.Agents.Domain;

/// <summary>
/// Represents the execution status of an agent run.
/// </summary>
public enum AgentRunStatus
{
    /// <summary>The run has been created but not yet started.</summary>
    Pending,

    /// <summary>The run is currently executing.</summary>
    Running,

    /// <summary>The run completed successfully.</summary>
    Completed,

    /// <summary>The run failed due to an error.</summary>
    Failed,

    /// <summary>The run was cancelled by the user.</summary>
    Cancelled
}

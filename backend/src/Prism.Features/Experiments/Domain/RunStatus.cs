namespace Prism.Features.Experiments.Domain;

/// <summary>
/// Represents the execution status of an experiment run.
/// </summary>
public enum RunStatus
{
    /// <summary>
    /// The run is queued but has not started executing.
    /// </summary>
    Pending,

    /// <summary>
    /// The run is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// The run completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The run failed due to an error.
    /// </summary>
    Failed
}

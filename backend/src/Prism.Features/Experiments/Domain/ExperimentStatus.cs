namespace Prism.Features.Experiments.Domain;

/// <summary>
/// Represents the lifecycle status of an experiment.
/// </summary>
public enum ExperimentStatus
{
    /// <summary>
    /// The experiment is actively being worked on.
    /// </summary>
    Active,

    /// <summary>
    /// The experiment has been completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The experiment has been archived and is no longer active.
    /// </summary>
    Archived
}

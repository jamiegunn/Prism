namespace Prism.Features.Experiments.Application.DeleteRun;

/// <summary>
/// Command to delete a run from an experiment.
/// </summary>
/// <param name="ExperimentId">The parent experiment ID.</param>
/// <param name="RunId">The run identifier.</param>
public sealed record DeleteRunCommand(Guid ExperimentId, Guid RunId);

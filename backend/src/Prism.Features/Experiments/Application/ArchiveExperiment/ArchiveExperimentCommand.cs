using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ArchiveExperiment;

/// <summary>
/// Command to change the status of an experiment (archive, complete, or reactivate).
/// </summary>
/// <param name="ExperimentId">The experiment identifier.</param>
/// <param name="Status">The new status to set.</param>
public sealed record ArchiveExperimentCommand(Guid ExperimentId, ExperimentStatus Status);

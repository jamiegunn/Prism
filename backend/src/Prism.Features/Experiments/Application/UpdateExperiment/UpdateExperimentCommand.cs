namespace Prism.Features.Experiments.Application.UpdateExperiment;

/// <summary>
/// Command to update an existing experiment.
/// </summary>
/// <param name="ExperimentId">The experiment identifier.</param>
/// <param name="Name">The updated experiment name.</param>
/// <param name="Description">The updated experiment description.</param>
/// <param name="Hypothesis">The updated hypothesis.</param>
public sealed record UpdateExperimentCommand(Guid ExperimentId, string Name, string? Description, string? Hypothesis);

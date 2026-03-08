namespace Prism.Features.Experiments.Application.GetExperiment;

/// <summary>
/// Query to get a specific experiment by ID.
/// </summary>
/// <param name="ExperimentId">The experiment identifier.</param>
public sealed record GetExperimentQuery(Guid ExperimentId);

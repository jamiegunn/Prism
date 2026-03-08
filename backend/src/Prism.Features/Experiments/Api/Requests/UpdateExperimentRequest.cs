namespace Prism.Features.Experiments.Api.Requests;

/// <summary>
/// HTTP request body for updating an experiment.
/// </summary>
/// <param name="Name">The updated experiment name.</param>
/// <param name="Description">The updated experiment description.</param>
/// <param name="Hypothesis">The updated hypothesis.</param>
public sealed record UpdateExperimentRequest(string Name, string? Description = null, string? Hypothesis = null);

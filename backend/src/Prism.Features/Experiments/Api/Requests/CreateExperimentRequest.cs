namespace Prism.Features.Experiments.Api.Requests;

/// <summary>
/// HTTP request body for creating a new experiment.
/// </summary>
/// <param name="ProjectId">The parent project ID.</param>
/// <param name="Name">The experiment name.</param>
/// <param name="Description">The optional experiment description.</param>
/// <param name="Hypothesis">The optional hypothesis being tested.</param>
public sealed record CreateExperimentRequest(Guid ProjectId, string Name, string? Description = null, string? Hypothesis = null);

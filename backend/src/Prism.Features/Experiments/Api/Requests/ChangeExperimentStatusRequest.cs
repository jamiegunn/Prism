namespace Prism.Features.Experiments.Api.Requests;

/// <summary>
/// HTTP request body for changing an experiment's status.
/// </summary>
/// <param name="Status">The new status (Active, Completed, or Archived).</param>
public sealed record ChangeExperimentStatusRequest(string Status);

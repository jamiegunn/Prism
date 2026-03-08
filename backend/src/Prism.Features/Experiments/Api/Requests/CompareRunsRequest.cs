namespace Prism.Features.Experiments.Api.Requests;

/// <summary>
/// HTTP request body for comparing runs.
/// </summary>
/// <param name="RunIds">The IDs of runs to compare.</param>
public sealed record CompareRunsRequest(List<Guid> RunIds);

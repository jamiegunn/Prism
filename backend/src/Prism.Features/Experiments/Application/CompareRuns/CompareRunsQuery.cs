namespace Prism.Features.Experiments.Application.CompareRuns;

/// <summary>
/// Query to compare multiple runs side-by-side.
/// </summary>
/// <param name="ExperimentId">The parent experiment ID.</param>
/// <param name="RunIds">The IDs of runs to compare.</param>
public sealed record CompareRunsQuery(Guid ExperimentId, List<Guid> RunIds);

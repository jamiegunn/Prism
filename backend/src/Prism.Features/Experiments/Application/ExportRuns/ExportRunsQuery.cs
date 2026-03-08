namespace Prism.Features.Experiments.Application.ExportRuns;

/// <summary>
/// Query to export runs from an experiment in a specified format.
/// </summary>
/// <param name="ExperimentId">The parent experiment ID.</param>
/// <param name="Format">The export format ("csv" or "json").</param>
public sealed record ExportRunsQuery(Guid ExperimentId, string Format);

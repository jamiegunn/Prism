using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.Dtos;

/// <summary>
/// Data transfer object for a comparison between multiple runs.
/// </summary>
/// <param name="Runs">The runs being compared.</param>
/// <param name="ParameterDiffs">Parameter differences across runs.</param>
/// <param name="MetricComparison">Metric values across all runs for comparison.</param>
public sealed record RunComparisonDto(
    List<RunDto> Runs,
    Dictionary<string, List<string?>> ParameterDiffs,
    Dictionary<string, List<double?>> MetricComparison);

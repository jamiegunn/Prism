namespace Prism.Features.Analytics.Application.Dtos;

/// <summary>
/// Performance metrics summary.
/// </summary>
public sealed record PerformanceSummaryDto(
    double AverageLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double? AverageTtftMs,
    double? AverageTokensPerSecond,
    List<PerformanceByModelDto> ByModel);

/// <summary>
/// Performance metrics by model.
/// </summary>
public sealed record PerformanceByModelDto(
    string Model,
    int RequestCount,
    double AverageLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double? AverageTtftMs,
    double? AverageTokensPerSecond);

namespace Prism.Features.Analytics.Application.Dtos;

/// <summary>
/// Summary of usage statistics over a time period.
/// </summary>
public sealed record UsageSummaryDto(
    int TotalRequests,
    long TotalPromptTokens,
    long TotalCompletionTokens,
    long TotalTokens,
    decimal? TotalCost,
    List<UsageByModelDto> ByModel,
    List<UsageByModuleDto> ByModule,
    List<UsageTimeSeriesDto> TimeSeries);

/// <summary>
/// Usage breakdown by model.
/// </summary>
public sealed record UsageByModelDto(
    string Model,
    int RequestCount,
    long TotalTokens,
    decimal? TotalCost);

/// <summary>
/// Usage breakdown by source module.
/// </summary>
public sealed record UsageByModuleDto(
    string Module,
    int RequestCount,
    long TotalTokens);

/// <summary>
/// Usage time series data point.
/// </summary>
public sealed record UsageTimeSeriesDto(
    DateTime Date,
    int RequestCount,
    long TotalTokens);

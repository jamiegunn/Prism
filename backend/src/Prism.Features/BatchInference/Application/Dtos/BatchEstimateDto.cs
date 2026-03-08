namespace Prism.Features.BatchInference.Application.Dtos;

/// <summary>
/// Estimated cost and time for a batch inference job.
/// </summary>
public sealed record BatchEstimateDto(
    int RecordCount,
    int EstimatedTokens,
    double EstimatedMinutes,
    string Model);

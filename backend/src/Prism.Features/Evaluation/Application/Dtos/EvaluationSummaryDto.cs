namespace Prism.Features.Evaluation.Application.Dtos;

/// <summary>
/// Aggregated scores summary for an evaluation, grouped by model and scoring method.
/// </summary>
public sealed record EvaluationSummaryDto(
    Guid EvaluationId,
    List<ModelSummaryDto> ModelSummaries);

/// <summary>
/// Summary of one model's performance in an evaluation.
/// </summary>
public sealed record ModelSummaryDto(
    string Model,
    int RecordCount,
    Dictionary<string, double> AverageScores,
    double AverageLatencyMs,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    int ErrorCount);

/// <summary>
/// Leaderboard entry ranking a model's performance.
/// </summary>
public sealed record LeaderboardEntryDto(
    Guid EvaluationId,
    string EvaluationName,
    string Model,
    Dictionary<string, double> AverageScores,
    int RecordCount,
    double AverageLatencyMs,
    DateTime EvaluatedAt);

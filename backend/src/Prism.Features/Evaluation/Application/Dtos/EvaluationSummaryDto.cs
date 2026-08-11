namespace Prism.Features.Evaluation.Application.Dtos;

/// <summary>
/// Aggregated scores summary for an evaluation, grouped by model and scoring method.
/// </summary>
/// <param name="EvaluationId">The evaluation identifier.</param>
/// <param name="ModelSummaries">One summary per evaluated model.</param>
/// <param name="ScoreDefinitions">The definition of each metric that appears in the
/// summaries, keyed by metric name — recorded when the evaluation ran, so the numbers stay
/// citable. Includes corpus-level metrics.</param>
public sealed record EvaluationSummaryDto(
    Guid EvaluationId,
    List<ModelSummaryDto> ModelSummaries,
    Dictionary<string, string> ScoreDefinitions);

/// <summary>
/// Summary of one model's performance in an evaluation.
/// </summary>
/// <param name="Model">The model name.</param>
/// <param name="RecordCount">How many records were attempted for this model.</param>
/// <param name="AverageScores">Mean per-item score per scoring method, over successful items
/// that have that score. These are means of sentence-level scores.</param>
/// <param name="CorpusMetrics">Corpus-level metrics computed from pooled statistics — not
/// means of per-item scores. Currently <c>corpus_bleu</c> when BLEU was scored. Absent keys
/// mean "not computed", never zero.</param>
/// <param name="AverageLatencyMs">Mean latency over successful items, or null when nothing
/// succeeded — a latency of no calls is not 0 ms.</param>
/// <param name="TotalPromptTokens">Prompt tokens summed over successful items.</param>
/// <param name="TotalCompletionTokens">Completion tokens summed over successful items.</param>
/// <param name="ErrorCount">How many items failed.</param>
public sealed record ModelSummaryDto(
    string Model,
    int RecordCount,
    Dictionary<string, double> AverageScores,
    Dictionary<string, double> CorpusMetrics,
    double? AverageLatencyMs,
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

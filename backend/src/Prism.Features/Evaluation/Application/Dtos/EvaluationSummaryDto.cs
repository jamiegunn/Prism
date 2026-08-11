namespace Prism.Features.Evaluation.Application.Dtos;

/// <summary>
/// Aggregated scores summary for an evaluation, grouped by model and scoring method.
/// </summary>
/// <param name="EvaluationId">The evaluation identifier.</param>
/// <param name="ModelSummaries">One summary per evaluated model.</param>
/// <param name="ScoreDefinitions">The definition of each metric that appears in the
/// summaries, keyed by metric name — recorded when the evaluation ran, so the numbers stay
/// citable. Includes corpus-level metrics.</param>
/// <param name="Comparisons">Paired statistical comparisons between each pair of evaluated
/// models, per metric — empty with fewer than two models or fewer than two shared items.</param>
public sealed record EvaluationSummaryDto(
    Guid EvaluationId,
    List<ModelSummaryDto> ModelSummaries,
    Dictionary<string, string> ScoreDefinitions,
    List<ModelComparisonDto> Comparisons);

/// <summary>
/// A Student-t 95% confidence interval on a mean per-item score.
/// </summary>
/// <param name="Mean">The sample mean (equal to the corresponding average score).</param>
/// <param name="Lower">Lower bound of the interval.</param>
/// <param name="Upper">Upper bound of the interval.</param>
/// <param name="StdDev">Sample standard deviation (n−1).</param>
/// <param name="SampleCount">How many per-item scores the interval covers.</param>
public sealed record ScoreIntervalDto(
    double Mean,
    double Lower,
    double Upper,
    double StdDev,
    int SampleCount);

/// <summary>
/// A paired two-sided Student-t comparison of two models on one metric, over the dataset
/// items both models scored — pairing is by dataset record, never by list position.
/// </summary>
/// <param name="Metric">The metric compared.</param>
/// <param name="ModelA">The first model (differences are A − B).</param>
/// <param name="ModelB">The second model.</param>
/// <param name="PairCount">How many items both models scored on this metric.</param>
/// <param name="MeanDifference">Mean per-item difference, A − B.</param>
/// <param name="Lower">Lower bound of the 95% CI on the mean difference.</param>
/// <param name="Upper">Upper bound of the 95% CI on the mean difference.</param>
/// <param name="TStatistic">The paired t statistic, or null when every pair differs by the
/// same amount (undefined, not zero).</param>
/// <param name="PValue">Two-sided p-value, or null when the statistic is undefined.</param>
public sealed record ModelComparisonDto(
    string Metric,
    string ModelA,
    string ModelB,
    int PairCount,
    double MeanDifference,
    double Lower,
    double Upper,
    double? TStatistic,
    double? PValue);

/// <summary>
/// Summary of one model's performance in an evaluation.
/// </summary>
/// <param name="Model">The model name.</param>
/// <param name="RecordCount">How many records were attempted for this model.</param>
/// <param name="AverageScores">Mean per-item score per scoring method, over successful items
/// that have that score. These are means of sentence-level scores.</param>
/// <param name="ScoreIntervals">Student-t 95% confidence interval per scoring method, keyed
/// like <paramref name="AverageScores"/>. A metric with fewer than two scored items has no
/// entry — one number has no measurable uncertainty, and no interval is not a zero-width
/// interval.</param>
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
    Dictionary<string, ScoreIntervalDto> ScoreIntervals,
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

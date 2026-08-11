using Microsoft.EntityFrameworkCore;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Domain;
using Prism.Features.Evaluation.Domain.Scorers;

namespace Prism.Features.Evaluation.Application.GetEvaluationResults;

/// <summary>
/// Query to get aggregated evaluation results (summary by model).
/// </summary>
public sealed record GetEvaluationResultsQuery(Guid EvaluationId);

/// <summary>
/// Handles getting aggregated evaluation results.
/// </summary>
public sealed class GetEvaluationResultsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetEvaluationResultsHandler"/> class.
    /// </summary>
    public GetEvaluationResultsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get evaluation results query.
    /// </summary>
    public async Task<Result<EvaluationSummaryDto>> HandleAsync(GetEvaluationResultsQuery query, CancellationToken ct)
    {
        EvaluationEntity? evaluation = await _db.Set<EvaluationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == query.EvaluationId, ct);

        if (evaluation is null)
        {
            return Error.NotFound($"Evaluation {query.EvaluationId} not found.");
        }

        List<EvaluationResult> results = await _db.Set<EvaluationResult>()
            .AsNoTracking()
            .Where(r => r.EvaluationId == query.EvaluationId)
            .ToListAsync(ct);

        var scoreDefinitions = new Dictionary<string, string>(evaluation.ScoreDefinitions);
        bool anyCorpusBleu = false;

        List<ModelSummaryDto> modelSummaries = results
            .GroupBy(r => r.Model)
            .Select(g =>
            {
                List<EvaluationResult> modelResults = g.ToList();
                List<EvaluationResult> successResults = modelResults.Where(r => r.Error is null).ToList();

                // Average each scoring method across all successful results, with a
                // Student-t 95% CI wherever there are at least two values to support one.
                Dictionary<string, double> averageScores = new();
                Dictionary<string, ScoreIntervalDto> scoreIntervals = new();
                if (successResults.Count > 0)
                {
                    IEnumerable<string> allKeys = successResults.SelectMany(r => r.Scores.Keys).Distinct();
                    foreach (string key in allKeys)
                    {
                        List<double> values = successResults
                            .Where(r => r.Scores.ContainsKey(key))
                            .Select(r => r.Scores[key])
                            .ToList();
                        if (values.Count > 0)
                        {
                            averageScores[key] = values.Average();
                        }

                        StatisticalMetrics.ConfidenceInterval? interval =
                            StatisticalMetrics.MeanConfidenceInterval(values);
                        if (interval is not null)
                        {
                            scoreIntervals[key] = new ScoreIntervalDto(
                                interval.Mean,
                                interval.Lower,
                                interval.Upper,
                                interval.StdDev,
                                interval.SampleCount);
                        }
                    }
                }

                Dictionary<string, double> corpusMetrics = new();
                double? corpusBleu = ComputeCorpusBleu(successResults);
                if (corpusBleu is not null)
                {
                    corpusMetrics["corpus_bleu"] = corpusBleu.Value;
                    anyCorpusBleu = true;
                }

                return new ModelSummaryDto(
                    g.Key,
                    modelResults.Count,
                    averageScores,
                    scoreIntervals,
                    corpusMetrics,
                    successResults.Count > 0 ? successResults.Average(r => r.LatencyMs) : null,
                    successResults.Sum(r => r.PromptTokens),
                    successResults.Sum(r => r.CompletionTokens),
                    modelResults.Count(r => r.Error is not null));
            })
            .ToList();

        if (anyCorpusBleu)
        {
            scoreDefinitions["corpus_bleu"] =
                "Corpus BLEU-4 from n-gram statistics summed over all items (sacrebleu " +
                $"{SacreBleuMetric.ReferenceVersion} definition): tokenizer 13a, " +
                "case-sensitive, exp smoothing, full order 4, brevity penalty over corpus " +
                "lengths. Scale 0–1. This is not the mean of the per-item sentence BLEUs.";
        }

        List<ModelComparisonDto> comparisons = ComputeComparisons(results);

        if (modelSummaries.Any(m => m.ScoreIntervals.Count > 0))
        {
            scoreDefinitions["ci95"] =
                "95% Student-t confidence interval on the mean of per-item scores "
                + "(t-distribution with n−1 degrees of freedom, Bessel-corrected standard "
                + "deviation). Reported only where at least two items were scored; verified "
                + "against scipy.stats.t.interval.";
        }

        if (comparisons.Count > 0)
        {
            scoreDefinitions["paired comparison"] =
                "Two-sided paired Student-t test on per-item score differences, paired by "
                + "dataset item over the items both models scored. Reports the mean "
                + "difference, its 95% CI, the t statistic and the p-value; undefined "
                + "statistics (zero variance) are reported as absent, not zero. Verified "
                + "against scipy.stats.ttest_rel.";
        }

        return new EvaluationSummaryDto(
            query.EvaluationId, modelSummaries, scoreDefinitions, comparisons);
    }

    /// <summary>
    /// Computes paired comparisons for every unordered model pair and every metric they
    /// share. Pairing is by dataset record — only items both models scored count, so a model
    /// that failed half its calls is compared on the intersection, not padded with zeros.
    /// </summary>
    /// <param name="results">All per-item results of the evaluation.</param>
    /// <returns>The comparisons, ordered by metric then model names.</returns>
    private static List<ModelComparisonDto> ComputeComparisons(List<EvaluationResult> results)
    {
        var comparisons = new List<ModelComparisonDto>();

        // model -> recordId -> scores (successful items only; first result wins on the
        // pathological duplicate-record case).
        Dictionary<string, Dictionary<Guid, Dictionary<string, double>>> byModel = results
            .Where(r => r.Error is null)
            .GroupBy(r => r.Model)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(r => r.RecordId)
                    .ToDictionary(rg => rg.Key, rg => rg.First().Scores));

        List<string> models = byModel.Keys.OrderBy(m => m, StringComparer.Ordinal).ToList();

        for (int i = 0; i < models.Count; i++)
        {
            for (int j = i + 1; j < models.Count; j++)
            {
                Dictionary<Guid, Dictionary<string, double>> a = byModel[models[i]];
                Dictionary<Guid, Dictionary<string, double>> b = byModel[models[j]];

                List<Guid> sharedRecords = a.Keys.Intersect(b.Keys).OrderBy(id => id).ToList();
                IEnumerable<string> sharedMetrics = sharedRecords
                    .SelectMany(id => a[id].Keys.Intersect(b[id].Keys))
                    .Distinct()
                    .OrderBy(m => m, StringComparer.Ordinal);

                foreach (string metric in sharedMetrics)
                {
                    List<double> aValues = [];
                    List<double> bValues = [];
                    foreach (Guid recordId in sharedRecords)
                    {
                        if (a[recordId].TryGetValue(metric, out double av)
                            && b[recordId].TryGetValue(metric, out double bv))
                        {
                            aValues.Add(av);
                            bValues.Add(bv);
                        }
                    }

                    StatisticalMetrics.PairedComparisonResult? cmp =
                        StatisticalMetrics.PairedComparison(aValues, bValues);
                    if (cmp is not null)
                    {
                        comparisons.Add(new ModelComparisonDto(
                            metric,
                            models[i],
                            models[j],
                            cmp.PairCount,
                            cmp.MeanDifference,
                            cmp.Lower,
                            cmp.Upper,
                            cmp.TStatistic,
                            cmp.PValue));
                    }
                }
            }
        }

        return comparisons
            .OrderBy(c => c.Metric, StringComparer.Ordinal)
            .ThenBy(c => c.ModelA, StringComparer.Ordinal)
            .ThenBy(c => c.ModelB, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Computes corpus BLEU over the items BLEU was scored on, by summing per-segment n-gram
    /// statistics — the sacrebleu corpus definition. Returns null when BLEU was not scored on
    /// any item: a corpus metric over nothing is absent, not zero.
    /// </summary>
    /// <param name="successResults">The successful per-item results for one model.</param>
    /// <returns>Corpus BLEU on the 0–1 scale, or null.</returns>
    private static double? ComputeCorpusBleu(List<EvaluationResult> successResults)
    {
        SacreBleuMetric.BleuStatistics? summed = null;

        foreach (EvaluationResult result in successResults)
        {
            if (!result.Scores.ContainsKey("bleu")
                || result.ExpectedOutput is null
                || result.ActualOutput is null)
            {
                continue;
            }

            SacreBleuMetric.BleuStatistics stats = SacreBleuMetric.SegmentStatistics(
                result.ActualOutput, result.ExpectedOutput);

            summed = summed is null ? stats : summed.Add(stats);
        }

        if (summed is null)
        {
            return null;
        }

        return SacreBleuMetric.ComputeBleu(summed, effectiveOrder: false).Score / 100.0;
    }
}

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

                // Average each scoring method across all successful results
                Dictionary<string, double> averageScores = new();
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

        return new EvaluationSummaryDto(query.EvaluationId, modelSummaries, scoreDefinitions);
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

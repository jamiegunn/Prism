using Microsoft.EntityFrameworkCore;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Domain;

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
        bool exists = await _db.Set<EvaluationEntity>()
            .AnyAsync(e => e.Id == query.EvaluationId, ct);

        if (!exists)
        {
            return Error.NotFound($"Evaluation {query.EvaluationId} not found.");
        }

        List<EvaluationResult> results = await _db.Set<EvaluationResult>()
            .AsNoTracking()
            .Where(r => r.EvaluationId == query.EvaluationId)
            .ToListAsync(ct);

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

                return new ModelSummaryDto(
                    g.Key,
                    modelResults.Count,
                    averageScores,
                    successResults.Count > 0 ? successResults.Average(r => r.LatencyMs) : 0,
                    successResults.Sum(r => r.PromptTokens),
                    successResults.Sum(r => r.CompletionTokens),
                    modelResults.Count(r => r.Error is not null));
            })
            .ToList();

        return new EvaluationSummaryDto(query.EvaluationId, modelSummaries);
    }
}

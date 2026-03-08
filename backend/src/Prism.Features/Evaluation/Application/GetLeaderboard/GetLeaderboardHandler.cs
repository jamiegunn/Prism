using Microsoft.EntityFrameworkCore;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.GetLeaderboard;

/// <summary>
/// Query to get a leaderboard of model performances across evaluations.
/// </summary>
public sealed record GetLeaderboardQuery(Guid? ProjectId, string? ScoringMethod);

/// <summary>
/// Handles getting the evaluation leaderboard.
/// </summary>
public sealed class GetLeaderboardHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLeaderboardHandler"/> class.
    /// </summary>
    public GetLeaderboardHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get leaderboard query.
    /// </summary>
    public async Task<Result<List<LeaderboardEntryDto>>> HandleAsync(GetLeaderboardQuery query, CancellationToken ct)
    {
        IQueryable<EvaluationEntity> evalQuery = _db.Set<EvaluationEntity>()
            .AsNoTracking()
            .Where(e => e.Status == EvaluationStatus.Completed);

        if (query.ProjectId.HasValue)
        {
            evalQuery = evalQuery.Where(e => e.ProjectId == query.ProjectId.Value);
        }

        List<EvaluationEntity> evaluations = await evalQuery.ToListAsync(ct);
        List<Guid> evaluationIds = evaluations.Select(e => e.Id).ToList();

        List<EvaluationResult> results = await _db.Set<EvaluationResult>()
            .AsNoTracking()
            .Where(r => evaluationIds.Contains(r.EvaluationId) && r.Error == null)
            .ToListAsync(ct);

        // Group by evaluation + model
        Dictionary<Guid, EvaluationEntity> evalLookup = evaluations.ToDictionary(e => e.Id);

        List<LeaderboardEntryDto> entries = results
            .GroupBy(r => new { r.EvaluationId, r.Model })
            .Select(g =>
            {
                EvaluationEntity eval = evalLookup[g.Key.EvaluationId];
                List<EvaluationResult> groupResults = g.ToList();

                Dictionary<string, double> averageScores = new();
                IEnumerable<string> allKeys = groupResults.SelectMany(r => r.Scores.Keys).Distinct();
                foreach (string key in allKeys)
                {
                    List<double> values = groupResults
                        .Where(r => r.Scores.ContainsKey(key))
                        .Select(r => r.Scores[key])
                        .ToList();
                    if (values.Count > 0)
                    {
                        averageScores[key] = values.Average();
                    }
                }

                return new LeaderboardEntryDto(
                    g.Key.EvaluationId,
                    eval.Name,
                    g.Key.Model,
                    averageScores,
                    groupResults.Count,
                    groupResults.Average(r => r.LatencyMs),
                    eval.CreatedAt);
            })
            .ToList();

        // Sort by primary scoring method if specified
        if (!string.IsNullOrWhiteSpace(query.ScoringMethod))
        {
            entries = entries
                .OrderByDescending(e => e.AverageScores.GetValueOrDefault(query.ScoringMethod))
                .ToList();
        }
        else
        {
            entries = entries.OrderByDescending(e => e.EvaluatedAt).ToList();
        }

        return entries;
    }
}

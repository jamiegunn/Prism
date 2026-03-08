using Microsoft.EntityFrameworkCore;
using Prism.Features.Analytics.Application.Dtos;
using Prism.Features.Analytics.Domain;

namespace Prism.Features.Analytics.Application.GetPerformance;

/// <summary>
/// Query to get performance metrics over a time period.
/// </summary>
public sealed record GetPerformanceQuery(DateTime? From, DateTime? To, string? Model);

/// <summary>
/// Handles getting performance metrics.
/// </summary>
public sealed class GetPerformanceHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPerformanceHandler"/> class.
    /// </summary>
    public GetPerformanceHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get performance query.
    /// </summary>
    public async Task<Result<PerformanceSummaryDto>> HandleAsync(GetPerformanceQuery query, CancellationToken ct)
    {
        IQueryable<UsageLog> q = _db.Set<UsageLog>().AsNoTracking();

        DateTime from = query.From ?? DateTime.UtcNow.AddDays(-30);
        DateTime to = query.To ?? DateTime.UtcNow;

        q = q.Where(l => l.CreatedAt >= from && l.CreatedAt <= to);

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            q = q.Where(l => l.Model == query.Model);
        }

        List<UsageLog> logs = await q.OrderBy(l => l.LatencyMs).ToListAsync(ct);

        if (logs.Count == 0)
        {
            return new PerformanceSummaryDto(0, 0, 0, 0, null, null, []);
        }

        List<PerformanceByModelDto> byModel = logs
            .GroupBy(l => l.Model)
            .Select(g =>
            {
                List<UsageLog> sorted = g.OrderBy(l => l.LatencyMs).ToList();
                return new PerformanceByModelDto(
                    g.Key,
                    sorted.Count,
                    sorted.Average(l => l.LatencyMs),
                    Percentile(sorted, 0.50),
                    Percentile(sorted, 0.95),
                    sorted.Where(l => l.TtftMs.HasValue).Select(l => (double)l.TtftMs!.Value).DefaultIfEmpty().Average(),
                    sorted.Where(l => l.TokensPerSecond.HasValue).Select(l => l.TokensPerSecond!.Value).DefaultIfEmpty().Average());
            })
            .OrderByDescending(m => m.RequestCount)
            .ToList();

        return new PerformanceSummaryDto(
            logs.Average(l => l.LatencyMs),
            Percentile(logs, 0.50),
            Percentile(logs, 0.95),
            Percentile(logs, 0.99),
            logs.Where(l => l.TtftMs.HasValue).Select(l => (double)l.TtftMs!.Value).DefaultIfEmpty().Average(),
            logs.Where(l => l.TokensPerSecond.HasValue).Select(l => l.TokensPerSecond!.Value).DefaultIfEmpty().Average(),
            byModel);
    }

    private static double Percentile(List<UsageLog> sortedLogs, double percentile)
    {
        if (sortedLogs.Count == 0) return 0;
        int index = (int)Math.Ceiling(percentile * sortedLogs.Count) - 1;
        return sortedLogs[Math.Max(0, index)].LatencyMs;
    }
}

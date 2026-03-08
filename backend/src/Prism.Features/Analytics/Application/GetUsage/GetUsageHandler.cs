using Microsoft.EntityFrameworkCore;
using Prism.Features.Analytics.Application.Dtos;
using Prism.Features.Analytics.Domain;

namespace Prism.Features.Analytics.Application.GetUsage;

/// <summary>
/// Query to get usage statistics over a time period.
/// </summary>
public sealed record GetUsageQuery(DateTime? From, DateTime? To, string? Model, string? SourceModule, Guid? ProjectId);

/// <summary>
/// Handles getting usage statistics.
/// </summary>
public sealed class GetUsageHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUsageHandler"/> class.
    /// </summary>
    public GetUsageHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get usage query.
    /// </summary>
    public async Task<Result<UsageSummaryDto>> HandleAsync(GetUsageQuery query, CancellationToken ct)
    {
        IQueryable<UsageLog> q = _db.Set<UsageLog>().AsNoTracking();

        DateTime from = query.From ?? DateTime.UtcNow.AddDays(-30);
        DateTime to = query.To ?? DateTime.UtcNow;

        q = q.Where(l => l.CreatedAt >= from && l.CreatedAt <= to);

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            q = q.Where(l => l.Model == query.Model);
        }

        if (!string.IsNullOrWhiteSpace(query.SourceModule))
        {
            q = q.Where(l => l.SourceModule == query.SourceModule);
        }

        if (query.ProjectId.HasValue)
        {
            q = q.Where(l => l.ProjectId == query.ProjectId.Value);
        }

        List<UsageLog> logs = await q.ToListAsync(ct);

        List<UsageByModelDto> byModel = logs
            .GroupBy(l => l.Model)
            .Select(g => new UsageByModelDto(
                g.Key,
                g.Count(),
                g.Sum(l => (long)l.PromptTokens + l.CompletionTokens),
                g.Sum(l => l.Cost)))
            .OrderByDescending(m => m.RequestCount)
            .ToList();

        List<UsageByModuleDto> byModule = logs
            .GroupBy(l => l.SourceModule)
            .Select(g => new UsageByModuleDto(
                g.Key,
                g.Count(),
                g.Sum(l => (long)l.PromptTokens + l.CompletionTokens)))
            .OrderByDescending(m => m.RequestCount)
            .ToList();

        List<UsageTimeSeriesDto> timeSeries = logs
            .GroupBy(l => l.CreatedAt.Date)
            .Select(g => new UsageTimeSeriesDto(
                g.Key,
                g.Count(),
                g.Sum(l => (long)l.PromptTokens + l.CompletionTokens)))
            .OrderBy(t => t.Date)
            .ToList();

        return new UsageSummaryDto(
            logs.Count,
            logs.Sum(l => (long)l.PromptTokens),
            logs.Sum(l => (long)l.CompletionTokens),
            logs.Sum(l => (long)l.PromptTokens + l.CompletionTokens),
            logs.Sum(l => l.Cost),
            byModel,
            byModule,
            timeSeries);
    }
}

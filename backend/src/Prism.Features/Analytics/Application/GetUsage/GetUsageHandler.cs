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

        // Aggregated in the database rather than by pulling every row into memory. The previous
        // version materialised the entire filtered table before grouping, so a month of real
        // traffic would have loaded millions of rows to produce a handful of numbers.
        //
        // Each group projects to an anonymous type and is shaped into its DTO afterwards: EF
        // cannot translate a grouping projected straight into a record constructor, and the
        // result sets here are one row per model, module or day.
        List<UsageByModelDto> byModel = (await q
                .GroupBy(l => l.Model)
                .Select(g => new
                {
                    Model = g.Key,
                    Requests = g.Count(),
                    Tokens = g.Sum(l => (long)l.PromptTokens + l.CompletionTokens),
                    Cost = g.Sum(l => l.Cost),

                    // EF translates SUM over a nullable column as COALESCE(SUM(...), 0), so a
                    // model with no pricing at all would report 0.00 — a claim that it was free
                    // rather than that its cost is unknown. Counting the priced rows is what
                    // lets that distinction survive the query.
                    PricedRows = g.Count(l => l.Cost != null),
                })
                .OrderByDescending(m => m.Requests)
                .ToListAsync(ct))
            .Select(m => new UsageByModelDto(
                m.Model, m.Requests, m.Tokens, m.PricedRows > 0 ? m.Cost : null))
            .ToList();

        List<UsageByModuleDto> byModule = (await q
                .GroupBy(l => l.SourceModule)
                .Select(g => new
                {
                    Module = g.Key,
                    Requests = g.Count(),
                    Tokens = g.Sum(l => (long)l.PromptTokens + l.CompletionTokens),
                })
                .OrderByDescending(m => m.Requests)
                .ToListAsync(ct))
            .Select(m => new UsageByModuleDto(m.Module, m.Requests, m.Tokens))
            .ToList();

        List<UsageTimeSeriesDto> timeSeries = (await q
                .GroupBy(l => l.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Requests = g.Count(),
                    Tokens = g.Sum(l => (long)l.PromptTokens + l.CompletionTokens),
                })
                .OrderBy(t => t.Date)
                .ToListAsync(ct))
            .Select(t => new UsageTimeSeriesDto(t.Date, t.Requests, t.Tokens))
            .ToList();

        // Totals derived from the per-model aggregate rather than a fourth query. EF cannot
        // translate GroupBy(_ => 1) into a whole-table aggregate, and ByModel already partitions
        // every row exactly once, so summing it is both correct and free.
        int totalRequests = byModel.Sum(m => m.RequestCount);
        long totalTokens = byModel.Sum(m => m.TotalTokens);

        long totalPromptTokens = await q.SumAsync(l => (long)l.PromptTokens, ct);
        long totalCompletionTokens = totalTokens - totalPromptTokens;

        // Null when nothing was priced, rather than a zero that reads as "this was free".
        decimal? totalCost = byModel.Any(m => m.TotalCost.HasValue)
            ? byModel.Sum(m => m.TotalCost ?? 0m)
            : null;

        return new UsageSummaryDto(
            totalRequests,
            totalPromptTokens,
            totalCompletionTokens,
            totalTokens,
            totalCost,
            byModel,
            byModule,
            timeSeries);
    }
}

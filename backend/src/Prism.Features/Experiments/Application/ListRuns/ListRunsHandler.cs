using Microsoft.EntityFrameworkCore;
using Prism.Common.Abstractions;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ListRuns;

/// <summary>
/// Handles listing runs in an experiment with filtering, sorting, and pagination.
/// </summary>
public sealed class ListRunsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListRunsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListRunsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a paginated list of runs for an experiment with optional filters and sorting.
    /// </summary>
    /// <param name="query">The query containing filter, sort, and pagination parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the paged run DTOs.</returns>
    public async Task<Result<PagedResult<RunDto>>> HandleAsync(ListRunsQuery query, CancellationToken ct)
    {
        bool experimentExists = await _db.Set<Experiment>()
            .AnyAsync(e => e.Id == query.ExperimentId, ct);

        if (!experimentExists)
        {
            return Error.NotFound($"Experiment '{query.ExperimentId}' was not found.");
        }

        IQueryable<Run> queryable = _db.Set<Run>()
            .AsNoTracking()
            .Where(r => r.ExperimentId == query.ExperimentId);

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            queryable = queryable.Where(r => r.Model == query.Model);
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(r => r.Status == query.Status.Value);
        }

        // Apply sorting
        bool ascending = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        queryable = query.SortBy.ToLowerInvariant() switch
        {
            "model" => ascending ? queryable.OrderBy(r => r.Model) : queryable.OrderByDescending(r => r.Model),
            "latencyms" or "latency" => ascending ? queryable.OrderBy(r => r.LatencyMs) : queryable.OrderByDescending(r => r.LatencyMs),
            "totaltokens" or "tokens" => ascending ? queryable.OrderBy(r => r.TotalTokens) : queryable.OrderByDescending(r => r.TotalTokens),
            "cost" => ascending ? queryable.OrderBy(r => r.Cost) : queryable.OrderByDescending(r => r.Cost),
            "perplexity" => ascending ? queryable.OrderBy(r => r.Perplexity) : queryable.OrderByDescending(r => r.Perplexity),
            "tokenspersecond" => ascending ? queryable.OrderBy(r => r.TokensPerSecond) : queryable.OrderByDescending(r => r.TokensPerSecond),
            _ => ascending ? queryable.OrderBy(r => r.CreatedAt) : queryable.OrderByDescending(r => r.CreatedAt)
        };

        int totalCount = await queryable.CountAsync(ct);

        List<Run> runs = await queryable
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        List<RunDto> items = runs.Select(RunDto.FromEntity).ToList();

        return new PagedResult<RunDto>(items, totalCount, query.Page, query.PageSize);
    }
}

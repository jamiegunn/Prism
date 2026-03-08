using Microsoft.EntityFrameworkCore;
using Prism.Common.Abstractions;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Application.GetBatchResults;

/// <summary>
/// Query to get paginated batch results.
/// </summary>
public sealed record GetBatchResultsQuery(Guid BatchJobId, string? Status, int Page, int PageSize);

/// <summary>
/// Handles getting paginated batch results.
/// </summary>
public sealed class GetBatchResultsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetBatchResultsHandler"/> class.
    /// </summary>
    public GetBatchResultsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get batch results query.
    /// </summary>
    public async Task<Result<PagedResult<BatchResultDto>>> HandleAsync(GetBatchResultsQuery query, CancellationToken ct)
    {
        bool exists = await _db.Set<BatchJob>()
            .AnyAsync(j => j.Id == query.BatchJobId, ct);

        if (!exists)
        {
            return Error.NotFound($"Batch job {query.BatchJobId} not found.");
        }

        IQueryable<BatchResult> q = _db.Set<BatchResult>()
            .AsNoTracking()
            .Where(r => r.BatchJobId == query.BatchJobId);

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<BatchResultStatus>(query.Status, true, out BatchResultStatus status))
        {
            q = q.Where(r => r.Status == status);
        }

        int totalCount = await q.CountAsync(ct);

        List<BatchResult> results = await q
            .OrderBy(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<BatchResultDto>(
            results.Select(BatchResultDto.FromEntity).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }
}

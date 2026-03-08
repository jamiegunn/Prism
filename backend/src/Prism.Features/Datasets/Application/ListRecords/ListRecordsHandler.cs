using Microsoft.EntityFrameworkCore;
using Prism.Common.Abstractions;
using Prism.Common.Results;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.ListRecords;

/// <summary>
/// Query to list records in a dataset with pagination and optional split filter.
/// </summary>
/// <param name="DatasetId">The dataset identifier.</param>
/// <param name="SplitLabel">Optional split label filter.</param>
/// <param name="Page">The page number (1-based).</param>
/// <param name="PageSize">The page size.</param>
public sealed record ListRecordsQuery(Guid DatasetId, string? SplitLabel, int Page, int PageSize);

/// <summary>
/// Handles listing dataset records with pagination.
/// </summary>
public sealed class ListRecordsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListRecordsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListRecordsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists records in a dataset with pagination.
    /// </summary>
    /// <param name="query">The list records query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A paged result of record DTOs.</returns>
    public async Task<Result<PagedResult<DatasetRecordDto>>> HandleAsync(ListRecordsQuery query, CancellationToken ct)
    {
        bool datasetExists = await _db.Set<Dataset>().AnyAsync(d => d.Id == query.DatasetId, ct);
        if (!datasetExists)
        {
            return Error.NotFound($"Dataset {query.DatasetId} not found.");
        }

        IQueryable<DatasetRecord> q = _db.Set<DatasetRecord>()
            .AsNoTracking()
            .Where(r => r.DatasetId == query.DatasetId);

        if (!string.IsNullOrWhiteSpace(query.SplitLabel))
        {
            q = q.Where(r => r.SplitLabel == query.SplitLabel);
        }

        int totalCount = await q.CountAsync(ct);

        List<DatasetRecord> records = await q
            .OrderBy(r => r.OrderIndex)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<DatasetRecordDto>(
            records.Select(DatasetRecordDto.FromEntity).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }
}

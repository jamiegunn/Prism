using Microsoft.EntityFrameworkCore;
using Prism.Common.Abstractions;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.GetResultRecords;

/// <summary>
/// Query to get individual evaluation result records with pagination.
/// </summary>
public sealed record GetResultRecordsQuery(Guid EvaluationId, string? Model, int Page, int PageSize);

/// <summary>
/// Handles getting paginated evaluation result records.
/// </summary>
public sealed class GetResultRecordsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetResultRecordsHandler"/> class.
    /// </summary>
    public GetResultRecordsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get result records query.
    /// </summary>
    public async Task<Result<PagedResult<EvaluationResultDto>>> HandleAsync(GetResultRecordsQuery query, CancellationToken ct)
    {
        bool exists = await _db.Set<EvaluationEntity>()
            .AnyAsync(e => e.Id == query.EvaluationId, ct);

        if (!exists)
        {
            return Error.NotFound($"Evaluation {query.EvaluationId} not found.");
        }

        IQueryable<EvaluationResult> q = _db.Set<EvaluationResult>()
            .AsNoTracking()
            .Where(r => r.EvaluationId == query.EvaluationId);

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            q = q.Where(r => r.Model == query.Model);
        }

        int totalCount = await q.CountAsync(ct);

        List<EvaluationResult> results = await q
            .OrderBy(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<EvaluationResultDto>(
            results.Select(EvaluationResultDto.FromEntity).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }
}

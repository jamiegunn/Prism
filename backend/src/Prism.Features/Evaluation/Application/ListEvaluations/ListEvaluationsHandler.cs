using Microsoft.EntityFrameworkCore;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.ListEvaluations;

/// <summary>
/// Query to list evaluations with optional project filter.
/// </summary>
public sealed record ListEvaluationsQuery(Guid? ProjectId, string? Search);

/// <summary>
/// Handles listing evaluations.
/// </summary>
public sealed class ListEvaluationsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListEvaluationsHandler"/> class.
    /// </summary>
    public ListEvaluationsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the list evaluations query.
    /// </summary>
    public async Task<Result<List<EvaluationDto>>> HandleAsync(ListEvaluationsQuery query, CancellationToken ct)
    {
        IQueryable<EvaluationEntity> q = _db.Set<EvaluationEntity>()
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt);

        if (query.ProjectId.HasValue)
        {
            q = q.Where(e => e.ProjectId == query.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.ToLower();
            q = q.Where(e => e.Name.ToLower().Contains(search));
        }

        List<EvaluationEntity> evaluations = await q.ToListAsync(ct);
        return evaluations.Select(EvaluationDto.FromEntity).ToList();
    }
}

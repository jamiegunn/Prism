using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ListExperiments;

/// <summary>
/// Handles listing of experiments with optional filtering.
/// </summary>
public sealed class ListExperimentsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListExperimentsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListExperimentsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a list of experiments with run counts, optionally filtered by project or status.
    /// </summary>
    /// <param name="query">The query containing filter parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of experiment DTOs.</returns>
    public async Task<Result<List<ExperimentDto>>> HandleAsync(ListExperimentsQuery query, CancellationToken ct)
    {
        IQueryable<Experiment> queryable = _db.Set<Experiment>().AsNoTracking();

        if (query.ProjectId.HasValue)
        {
            queryable = queryable.Where(e => e.ProjectId == query.ProjectId.Value);
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(e => e.Status == query.Status.Value);
        }

        List<Experiment> experiments = await queryable
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);

        List<Guid> experimentIds = experiments.Select(e => e.Id).ToList();

        Dictionary<Guid, int> runCounts = await _db.Set<Run>()
            .Where(r => experimentIds.Contains(r.ExperimentId))
            .GroupBy(r => r.ExperimentId)
            .Select(g => new { ExperimentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExperimentId, x => x.Count, ct);

        List<ExperimentDto> dtos = experiments
            .Select(e => ExperimentDto.FromEntity(e, runCounts.GetValueOrDefault(e.Id, 0)))
            .ToList();

        return dtos;
    }
}

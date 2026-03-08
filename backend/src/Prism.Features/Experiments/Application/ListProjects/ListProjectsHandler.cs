using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ListProjects;

/// <summary>
/// Handles listing of research projects with optional filtering.
/// </summary>
public sealed class ListProjectsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListProjectsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListProjectsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a list of projects with experiment counts.
    /// </summary>
    /// <param name="query">The query containing filter parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of project DTOs.</returns>
    public async Task<Result<List<ProjectDto>>> HandleAsync(ListProjectsQuery query, CancellationToken ct)
    {
        IQueryable<Project> queryable = _db.Set<Project>().AsNoTracking();

        if (!query.IncludeArchived)
        {
            queryable = queryable.Where(p => !p.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.ToLowerInvariant();
            queryable = queryable.Where(p => p.Name.ToLower().Contains(search));
        }

        List<Project> projects = await queryable
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);

        List<Guid> projectIds = projects.Select(p => p.Id).ToList();

        Dictionary<Guid, int> experimentCounts = await _db.Set<Experiment>()
            .Where(e => projectIds.Contains(e.ProjectId))
            .GroupBy(e => e.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count, ct);

        List<ProjectDto> dtos = projects
            .Select(p => ProjectDto.FromEntity(p, experimentCounts.GetValueOrDefault(p.Id, 0)))
            .ToList();

        return dtos;
    }
}

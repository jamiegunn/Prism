using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.GetProject;

/// <summary>
/// Handles retrieval of a specific research project by ID.
/// </summary>
public sealed class GetProjectHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetProjectHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetProjectHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves a project by its ID, including experiment count.
    /// </summary>
    /// <param name="query">The query containing the project ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the project DTO on success.</returns>
    public async Task<Result<ProjectDto>> HandleAsync(GetProjectQuery query, CancellationToken ct)
    {
        Project? project = await _db.Set<Project>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.ProjectId, ct);

        if (project is null)
        {
            return Error.NotFound($"Project '{query.ProjectId}' was not found.");
        }

        int experimentCount = await _db.Set<Experiment>()
            .CountAsync(e => e.ProjectId == project.Id, ct);

        return ProjectDto.FromEntity(project, experimentCount);
    }
}

using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.UpdateProject;

/// <summary>
/// Handles updating an existing research project.
/// </summary>
public sealed class UpdateProjectHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateProjectHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProjectHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public UpdateProjectHandler(AppDbContext db, ILogger<UpdateProjectHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Updates the name and description of an existing project.
    /// </summary>
    /// <param name="command">The update project command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated project DTO on success.</returns>
    public async Task<Result<ProjectDto>> HandleAsync(UpdateProjectCommand command, CancellationToken ct)
    {
        Project? project = await _db.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct);

        if (project is null)
        {
            return Error.NotFound($"Project '{command.ProjectId}' was not found.");
        }

        project.Name = command.Name;
        project.Description = command.Description;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated project {ProjectId}", project.Id);

        int experimentCount = await _db.Set<Experiment>()
            .CountAsync(e => e.ProjectId == project.Id, ct);

        return ProjectDto.FromEntity(project, experimentCount);
    }
}

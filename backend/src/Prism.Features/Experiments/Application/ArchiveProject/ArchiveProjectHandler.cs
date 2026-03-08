using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ArchiveProject;

/// <summary>
/// Handles archiving or unarchiving a research project.
/// </summary>
public sealed class ArchiveProjectHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<ArchiveProjectHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchiveProjectHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ArchiveProjectHandler(AppDbContext db, ILogger<ArchiveProjectHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Archives or unarchives a project.
    /// </summary>
    /// <param name="command">The archive project command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated project DTO on success.</returns>
    public async Task<Result<ProjectDto>> HandleAsync(ArchiveProjectCommand command, CancellationToken ct)
    {
        Project? project = await _db.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct);

        if (project is null)
        {
            return Error.NotFound($"Project '{command.ProjectId}' was not found.");
        }

        project.IsArchived = command.Archive;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "{Action} project {ProjectId}",
            command.Archive ? "Archived" : "Unarchived",
            project.Id);

        int experimentCount = await _db.Set<Experiment>()
            .CountAsync(e => e.ProjectId == project.Id, ct);

        return ProjectDto.FromEntity(project, experimentCount);
    }
}

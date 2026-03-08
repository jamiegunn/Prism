namespace Prism.Features.Experiments.Application.ArchiveProject;

/// <summary>
/// Command to archive (or unarchive) a research project.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="Archive">True to archive, false to unarchive.</param>
public sealed record ArchiveProjectCommand(Guid ProjectId, bool Archive = true);

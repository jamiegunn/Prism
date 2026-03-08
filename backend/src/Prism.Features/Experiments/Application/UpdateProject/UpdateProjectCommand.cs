namespace Prism.Features.Experiments.Application.UpdateProject;

/// <summary>
/// Command to update an existing research project.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
/// <param name="Name">The updated project name.</param>
/// <param name="Description">The updated project description.</param>
public sealed record UpdateProjectCommand(Guid ProjectId, string Name, string? Description);

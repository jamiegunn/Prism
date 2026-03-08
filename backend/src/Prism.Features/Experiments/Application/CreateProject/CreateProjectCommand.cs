namespace Prism.Features.Experiments.Application.CreateProject;

/// <summary>
/// Command to create a new research project.
/// </summary>
/// <param name="Name">The project name.</param>
/// <param name="Description">The optional project description.</param>
public sealed record CreateProjectCommand(string Name, string? Description);

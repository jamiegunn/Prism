namespace Prism.Features.Experiments.Api.Requests;

/// <summary>
/// HTTP request body for creating a new research project.
/// </summary>
/// <param name="Name">The project name.</param>
/// <param name="Description">The optional project description.</param>
public sealed record CreateProjectRequest(string Name, string? Description = null);

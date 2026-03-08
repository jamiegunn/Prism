namespace Prism.Features.Experiments.Api.Requests;

/// <summary>
/// HTTP request body for updating a research project.
/// </summary>
/// <param name="Name">The updated project name.</param>
/// <param name="Description">The updated project description.</param>
public sealed record UpdateProjectRequest(string Name, string? Description = null);

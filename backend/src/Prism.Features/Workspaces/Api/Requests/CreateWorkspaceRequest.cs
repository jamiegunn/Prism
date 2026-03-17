namespace Prism.Features.Workspaces.Api.Requests;

/// <summary>
/// Request body for creating a new workspace.
/// </summary>
/// <param name="Name">The workspace name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="IconColor">Optional icon color identifier.</param>
public sealed record CreateWorkspaceRequest(string Name, string? Description = null, string? IconColor = null);

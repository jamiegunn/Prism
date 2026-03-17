using Prism.Features.Workspaces.Domain;

namespace Prism.Features.Workspaces.Application.Dtos;

/// <summary>
/// Data transfer object for a workspace.
/// </summary>
public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDefault,
    string? IconColor,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Maps a <see cref="Workspace"/> entity to a <see cref="WorkspaceDto"/>.
    /// </summary>
    /// <param name="entity">The workspace entity.</param>
    /// <returns>The mapped DTO.</returns>
    public static WorkspaceDto FromEntity(Workspace entity) => new(
        entity.Id,
        entity.Name,
        entity.Description,
        entity.IsDefault,
        entity.IconColor,
        entity.CreatedAt,
        entity.UpdatedAt);
}

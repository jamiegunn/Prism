using Prism.Features.Notebooks.Domain;

namespace Prism.Features.Notebooks.Application.Dtos;

/// <summary>
/// Data transfer object for a notebook (without full content for list views).
/// </summary>
public sealed record NotebookSummaryDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string? Description,
    int Version,
    long SizeBytes,
    string KernelName,
    DateTime? LastEditedAt,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a summary DTO from a <see cref="Notebook"/> entity.
    /// </summary>
    /// <param name="entity">The notebook entity.</param>
    /// <returns>A new <see cref="NotebookSummaryDto"/>.</returns>
    public static NotebookSummaryDto FromEntity(Notebook entity) => new(
        entity.Id,
        entity.ProjectId,
        entity.Name,
        entity.Description,
        entity.Version,
        entity.SizeBytes,
        entity.KernelName,
        entity.LastEditedAt,
        entity.CreatedAt);
}

/// <summary>
/// Data transfer object for a notebook including full .ipynb content.
/// </summary>
public sealed record NotebookDetailDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string? Description,
    string Content,
    int Version,
    long SizeBytes,
    string KernelName,
    DateTime? LastEditedAt,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a detail DTO from a <see cref="Notebook"/> entity.
    /// </summary>
    /// <param name="entity">The notebook entity.</param>
    /// <returns>A new <see cref="NotebookDetailDto"/>.</returns>
    public static NotebookDetailDto FromEntity(Notebook entity) => new(
        entity.Id,
        entity.ProjectId,
        entity.Name,
        entity.Description,
        entity.Content,
        entity.Version,
        entity.SizeBytes,
        entity.KernelName,
        entity.LastEditedAt,
        entity.CreatedAt);
}

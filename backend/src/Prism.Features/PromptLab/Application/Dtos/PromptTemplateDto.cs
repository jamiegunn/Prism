using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.Dtos;

/// <summary>
/// Data transfer object for a prompt template.
/// </summary>
/// <param name="Id">The unique template identifier.</param>
/// <param name="ProjectId">The optional associated project ID.</param>
/// <param name="Name">The template name.</param>
/// <param name="Category">The optional category.</param>
/// <param name="Description">The optional description.</param>
/// <param name="Tags">The template tags.</param>
/// <param name="LatestVersion">The latest version number.</param>
/// <param name="CreatedAt">The UTC timestamp when the template was created.</param>
/// <param name="UpdatedAt">The UTC timestamp when the template was last updated.</param>
public sealed record PromptTemplateDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string? Category,
    string? Description,
    List<string> Tags,
    int LatestVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates a <see cref="PromptTemplateDto"/> from a <see cref="PromptTemplate"/> entity.
    /// </summary>
    /// <param name="entity">The template entity to map.</param>
    /// <returns>A new <see cref="PromptTemplateDto"/> instance.</returns>
    public static PromptTemplateDto FromEntity(PromptTemplate entity)
    {
        return new PromptTemplateDto(
            entity.Id,
            entity.ProjectId,
            entity.Name,
            entity.Category,
            entity.Description,
            entity.Tags,
            entity.LatestVersion,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}

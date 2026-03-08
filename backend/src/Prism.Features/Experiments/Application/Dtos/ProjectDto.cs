using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.Dtos;

/// <summary>
/// Data transfer object for a research project.
/// </summary>
/// <param name="Id">The unique project identifier.</param>
/// <param name="Name">The project name.</param>
/// <param name="Description">The optional project description.</param>
/// <param name="IsArchived">Whether the project is archived.</param>
/// <param name="ExperimentCount">The number of experiments in the project.</param>
/// <param name="CreatedAt">The UTC timestamp when the project was created.</param>
/// <param name="UpdatedAt">The UTC timestamp when the project was last updated.</param>
public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsArchived,
    int ExperimentCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates a <see cref="ProjectDto"/> from a <see cref="Project"/> entity.
    /// </summary>
    /// <param name="entity">The project entity to map.</param>
    /// <param name="experimentCount">The number of experiments in the project.</param>
    /// <returns>A new <see cref="ProjectDto"/> instance.</returns>
    public static ProjectDto FromEntity(Project entity, int experimentCount)
    {
        return new ProjectDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.IsArchived,
            experimentCount,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}

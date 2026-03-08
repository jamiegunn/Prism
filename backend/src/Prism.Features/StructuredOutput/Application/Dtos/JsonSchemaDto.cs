using Prism.Features.StructuredOutput.Domain;

namespace Prism.Features.StructuredOutput.Application.Dtos;

/// <summary>
/// Data transfer object for a JSON schema.
/// </summary>
public sealed record JsonSchemaDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string? Description,
    string SchemaJson,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    /// <param name="entity">The schema entity.</param>
    /// <returns>A new <see cref="JsonSchemaDto"/>.</returns>
    public static JsonSchemaDto FromEntity(JsonSchemaEntity entity) => new(
        entity.Id,
        entity.ProjectId,
        entity.Name,
        entity.Description,
        entity.SchemaJson,
        entity.Version,
        entity.CreatedAt,
        entity.UpdatedAt);
}

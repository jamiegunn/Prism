using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.Dtos;

/// <summary>
/// Data transfer object for a RAG collection.
/// </summary>
public sealed record RagCollectionDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string? Description,
    string EmbeddingModel,
    int Dimensions,
    string DistanceMetric,
    string ChunkingStrategy,
    int ChunkSize,
    int ChunkOverlap,
    int DocumentCount,
    int ChunkCount,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    /// <param name="entity">The collection entity.</param>
    /// <returns>A new <see cref="RagCollectionDto"/>.</returns>
    public static RagCollectionDto FromEntity(RagCollection entity) => new(
        entity.Id,
        entity.ProjectId,
        entity.Name,
        entity.Description,
        entity.EmbeddingModel,
        entity.Dimensions,
        entity.DistanceMetric.ToString(),
        entity.ChunkingStrategy,
        entity.ChunkSize,
        entity.ChunkOverlap,
        entity.DocumentCount,
        entity.ChunkCount,
        entity.Status.ToString(),
        entity.CreatedAt,
        entity.UpdatedAt);
}

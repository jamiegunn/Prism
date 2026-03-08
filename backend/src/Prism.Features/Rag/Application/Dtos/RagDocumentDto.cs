using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.Dtos;

/// <summary>
/// Data transfer object for a RAG document.
/// </summary>
public sealed record RagDocumentDto(
    Guid Id,
    Guid CollectionId,
    string Filename,
    string ContentType,
    long SizeBytes,
    int ChunkCount,
    int CharacterCount,
    Dictionary<string, string> Metadata,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    /// <param name="entity">The document entity.</param>
    /// <returns>A new <see cref="RagDocumentDto"/>.</returns>
    public static RagDocumentDto FromEntity(RagDocument entity) => new(
        entity.Id,
        entity.CollectionId,
        entity.Filename,
        entity.ContentType,
        entity.SizeBytes,
        entity.ChunkCount,
        entity.CharacterCount,
        entity.Metadata,
        entity.Status.ToString(),
        entity.ErrorMessage,
        entity.CreatedAt);
}

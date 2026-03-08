namespace Prism.Features.Rag.Application.Dtos;

/// <summary>
/// Represents a chunk returned from a search query with its relevance score.
/// </summary>
public sealed record ChunkSearchResultDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentFilename,
    string Content,
    double Score,
    int OrderIndex,
    int TokenCount,
    Dictionary<string, string> Metadata);

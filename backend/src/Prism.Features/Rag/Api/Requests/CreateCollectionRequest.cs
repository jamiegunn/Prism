namespace Prism.Features.Rag.Api.Requests;

/// <summary>
/// Request body for creating a RAG collection.
/// </summary>
public sealed record CreateCollectionRequest(
    string Name,
    string? Description,
    string EmbeddingModel,
    int Dimensions,
    string? DistanceMetric,
    string? ChunkingStrategy,
    int? ChunkSize,
    int? ChunkOverlap,
    Guid? ProjectId);

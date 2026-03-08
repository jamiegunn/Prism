namespace Prism.Features.Rag.Application.Dtos;

/// <summary>
/// Result of a full RAG pipeline execution including retrieval and generation.
/// </summary>
public sealed record RagPipelineResultDto(
    string Query,
    string GeneratedResponse,
    List<ChunkSearchResultDto> RetrievedChunks,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    double LatencyMs,
    string RenderedPrompt);

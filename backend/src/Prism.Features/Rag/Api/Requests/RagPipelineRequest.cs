namespace Prism.Features.Rag.Api.Requests;

/// <summary>
/// Request body for executing the full RAG pipeline.
/// </summary>
public sealed record RagPipelineRequest(
    string Query,
    string Model,
    Guid InstanceId,
    string? SystemPrompt,
    string? PromptTemplate,
    int TopK = 5,
    string SearchType = "vector",
    double? Temperature = null,
    int? MaxTokens = null);

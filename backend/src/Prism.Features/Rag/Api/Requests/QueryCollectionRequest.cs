namespace Prism.Features.Rag.Api.Requests;

/// <summary>
/// Request body for querying a RAG collection.
/// </summary>
public sealed record QueryCollectionRequest(
    string QueryText,
    int TopK = 5,
    string SearchType = "vector",
    double VectorWeight = 0.7);

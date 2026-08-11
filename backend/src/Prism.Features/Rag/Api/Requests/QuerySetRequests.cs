namespace Prism.Features.Rag.Api.Requests;

/// <summary>
/// One labelled query in a create-query-set request.
/// </summary>
/// <param name="QueryText">The query text.</param>
/// <param name="RelevantChunkIds">The chunk ids relevant to the query.</param>
public sealed record CreateQuerySetItemRequest(string QueryText, List<Guid> RelevantChunkIds);

/// <summary>
/// Request body for creating a labelled query set.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Items">The labelled queries.</param>
public sealed record CreateQuerySetRequest(
    string Name,
    string? Description,
    List<CreateQuerySetItemRequest> Items);

/// <summary>
/// Request body for evaluating retrieval against a labelled query set.
/// </summary>
/// <param name="QuerySetId">The set to score against.</param>
/// <param name="TopK">Ranking depth; defaults to 10.</param>
/// <param name="Modes">Modes to evaluate (<c>vector</c>, <c>bm25</c>, <c>hybrid</c>);
/// null or empty means all three.</param>
public sealed record EvaluateRetrievalRequest(
    Guid QuerySetId,
    int TopK = 10,
    List<string>? Modes = null);

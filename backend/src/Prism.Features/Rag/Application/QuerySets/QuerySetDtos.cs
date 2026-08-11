namespace Prism.Features.Rag.Application.QuerySets;

/// <summary>
/// A labelled query set summary.
/// </summary>
/// <param name="Id">The set's identifier.</param>
/// <param name="CollectionId">The collection it labels.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="ItemCount">How many labelled queries it holds.</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
public sealed record RagQuerySetDto(
    Guid Id,
    Guid CollectionId,
    string Name,
    string? Description,
    int ItemCount,
    DateTime CreatedAt);

/// <summary>
/// One labelled query in a set.
/// </summary>
/// <param name="Id">The item's identifier.</param>
/// <param name="QueryText">The query text.</param>
/// <param name="RelevantChunkIds">The chunk ids labelled relevant.</param>
public sealed record RagQuerySetItemDto(
    Guid Id,
    string QueryText,
    List<Guid> RelevantChunkIds);

/// <summary>
/// A query set with its items.
/// </summary>
/// <param name="Id">The set's identifier.</param>
/// <param name="CollectionId">The collection it labels.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Items">The labelled queries.</param>
public sealed record RagQuerySetDetailDto(
    Guid Id,
    Guid CollectionId,
    string Name,
    string? Description,
    List<RagQuerySetItemDto> Items);

/// <summary>
/// The metrics for one retrieval mode over a query set. When the mode could not run at all,
/// <paramref name="Error"/> says why and <paramref name="Metrics"/> is null — a mode that
/// failed has no metrics, not zero metrics.
/// </summary>
/// <param name="Mode">The retrieval mode (vector, bm25, hybrid).</param>
/// <param name="QueryCount">How many labelled queries were scored.</param>
/// <param name="Metrics">Mean metric values keyed by metric name (e.g. <c>precision@5</c>,
/// <c>mrr</c>, <c>ndcg@10</c>), or null when the mode failed.</param>
/// <param name="Error">Why the mode failed, or null on success.</param>
public sealed record RetrievalModeResultDto(
    string Mode,
    int QueryCount,
    Dictionary<string, double>? Metrics,
    string? Error);

/// <summary>
/// The full evaluation of a collection's retrieval modes against a labelled query set.
/// </summary>
/// <param name="CollectionId">The collection evaluated.</param>
/// <param name="QuerySetId">The query set used.</param>
/// <param name="TopK">The ranking depth each mode was asked for.</param>
/// <param name="Modes">Per-mode results, in the order requested.</param>
/// <param name="Definitions">The definition of each metric, keyed by metric name.</param>
public sealed record RetrievalEvaluationDto(
    Guid CollectionId,
    Guid QuerySetId,
    int TopK,
    List<RetrievalModeResultDto> Modes,
    Dictionary<string, string> Definitions);

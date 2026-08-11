using Prism.Common.Database;

namespace Prism.Features.Rag.Domain;

/// <summary>
/// A labelled query set for a collection: queries with the chunk ids a correct retrieval
/// should return. The ground truth retrieval evaluation scores against.
/// </summary>
public sealed class RagQuerySet : BaseEntity
{
    /// <summary>
    /// Gets or sets the collection this query set labels. Chunk ids are only meaningful
    /// within one collection, so a set cannot span collections.
    /// </summary>
    public Guid CollectionId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets an optional description of what the set covers.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the labelled items.
    /// </summary>
    public List<RagQuerySetItem> Items { get; set; } = [];
}

/// <summary>
/// One labelled query: the text a user would search, and the chunks that are relevant to it.
/// </summary>
public sealed class RagQuerySetItem : BaseEntity
{
    /// <summary>
    /// Gets or sets the owning query set.
    /// </summary>
    public Guid QuerySetId { get; set; }

    /// <summary>
    /// Gets or sets the query text.
    /// </summary>
    public string QueryText { get; set; } = "";

    /// <summary>
    /// Gets or sets the ids of the chunks relevant to this query. Binary relevance: a chunk
    /// is in the set or it is not.
    /// </summary>
    public List<Guid> RelevantChunkIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the display order within the set.
    /// </summary>
    public int OrderIndex { get; set; }
}

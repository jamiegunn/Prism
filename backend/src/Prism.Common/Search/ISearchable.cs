namespace Prism.Common.Search;

/// <summary>
/// Marks an entity as participating in global search indexing.
/// Entities implementing this interface have their content indexed for full-text search
/// via PostgreSQL tsvector.
/// </summary>
public interface ISearchable
{
    /// <summary>
    /// Gets the entity type name used in search results (e.g., "prompt", "experiment").
    /// </summary>
    string SearchEntityType { get; }

    /// <summary>
    /// Gets the searchable title or name of the entity.
    /// </summary>
    string SearchTitle { get; }

    /// <summary>
    /// Gets the searchable text content of the entity, used for full-text indexing.
    /// </summary>
    string SearchContent { get; }
}

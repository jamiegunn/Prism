using Prism.Common.Abstractions;
using Prism.Common.Results;

namespace Prism.Common.Search;

/// <summary>
/// Defines the global search contract for full-text search across all entities in the application.
/// Backed by PostgreSQL tsvector for efficient text search.
/// </summary>
public interface IGlobalSearch
{
    /// <summary>
    /// Performs a full-text search across all indexed entities.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="entityTypes">Optional filter to restrict results to specific entity types.</param>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of results per page.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the paginated search results.</returns>
    Task<Result<PagedResult<SearchResult>>> SearchAsync(
        string query,
        IReadOnlyList<string>? entityTypes,
        int page,
        int pageSize,
        CancellationToken ct);
}

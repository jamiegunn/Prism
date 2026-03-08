namespace Prism.Common.Abstractions;

/// <summary>
/// Represents a paginated collection of items with metadata about the total result set.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
/// <param name="Items">The items in the current page.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
/// <param name="Page">The current one-based page number.</param>
/// <param name="PageSize">The number of items per page.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    /// <summary>
    /// Gets the total number of pages based on <see cref="TotalCount"/> and <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Creates an empty paged result.
    /// </summary>
    /// <param name="page">The requested page number.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <returns>An empty <see cref="PagedResult{T}"/>.</returns>
    public static PagedResult<T> Empty(int page = 1, int pageSize = 20) =>
        new(Array.Empty<T>(), 0, page, pageSize);
}

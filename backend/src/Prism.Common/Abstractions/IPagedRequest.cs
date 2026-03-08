namespace Prism.Common.Abstractions;

/// <summary>
/// Defines pagination and sorting parameters for list queries.
/// </summary>
public interface IPagedRequest
{
    /// <summary>
    /// Gets the one-based page number to retrieve.
    /// </summary>
    int Page { get; }

    /// <summary>
    /// Gets the number of items per page.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// Gets the optional property name to sort by.
    /// </summary>
    string? SortBy { get; }

    /// <summary>
    /// Gets the optional sort direction. Defaults to ascending if not specified.
    /// </summary>
    SortOrder? SortOrder { get; }
}

/// <summary>
/// Specifies the direction of sorting.
/// </summary>
public enum SortOrder
{
    /// <summary>Sort in ascending order.</summary>
    Ascending,

    /// <summary>Sort in descending order.</summary>
    Descending
}

namespace Prism.Features.Playground.Application.ListConversations;

/// <summary>
/// Query to retrieve a paginated list of playground conversations.
/// </summary>
/// <param name="Search">An optional search string to filter by title.</param>
/// <param name="Page">The one-based page number (default: 1).</param>
/// <param name="PageSize">The number of items per page (default: 20).</param>
public sealed record ListConversationsQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

namespace Prism.Features.History.Application.SearchHistory;

/// <summary>
/// Query to search and filter inference history records with pagination.
/// </summary>
/// <param name="Search">An optional text search applied to source module, model, and request/response JSON.</param>
/// <param name="SourceModule">An optional filter for the originating module.</param>
/// <param name="Model">An optional filter for the model identifier.</param>
/// <param name="From">An optional start date filter (inclusive).</param>
/// <param name="To">An optional end date filter (inclusive).</param>
/// <param name="Tags">An optional list of tags to filter by (records must contain all specified tags).</param>
/// <param name="IsSuccess">An optional filter for success/failure status.</param>
/// <param name="Page">The one-based page number (default: 1).</param>
/// <param name="PageSize">The number of items per page (default: 20).</param>
public sealed record SearchHistoryQuery(
    string? Search = null,
    string? SourceModule = null,
    string? Model = null,
    DateTime? From = null,
    DateTime? To = null,
    List<string>? Tags = null,
    bool? IsSuccess = null,
    int Page = 1,
    int PageSize = 20);

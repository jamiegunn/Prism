using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ListRuns;

/// <summary>
/// Query to list runs in an experiment with optional filtering, sorting, and pagination.
/// </summary>
/// <param name="ExperimentId">The parent experiment ID.</param>
/// <param name="Model">Optional model filter.</param>
/// <param name="Status">Optional status filter.</param>
/// <param name="Tags">Optional tag filter.</param>
/// <param name="SortBy">The field to sort by (default: createdAt).</param>
/// <param name="SortOrder">The sort order (asc or desc, default: desc).</param>
/// <param name="Page">The page number (default: 1).</param>
/// <param name="PageSize">The page size (default: 50).</param>
public sealed record ListRunsQuery(
    Guid ExperimentId,
    string? Model = null,
    RunStatus? Status = null,
    List<string>? Tags = null,
    string SortBy = "createdAt",
    string SortOrder = "desc",
    int Page = 1,
    int PageSize = 50);

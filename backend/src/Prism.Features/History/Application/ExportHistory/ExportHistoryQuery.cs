using Prism.Features.History.Application.SearchHistory;

namespace Prism.Features.History.Application.ExportHistory;

/// <summary>
/// Query to export filtered history records in a given file format.
/// </summary>
/// <param name="Filters">
/// The same filter set the search endpoint accepts. Pagination on the inner query is ignored:
/// an export selects every row the filters match, not one page of them.
/// </param>
/// <param name="Format">The requested format: <c>jsonl</c>, <c>csv</c> or <c>parquet</c>.</param>
public sealed record ExportHistoryQuery(SearchHistoryQuery Filters, string Format);

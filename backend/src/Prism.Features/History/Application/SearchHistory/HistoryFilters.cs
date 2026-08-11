using Prism.Features.History.Domain;

namespace Prism.Features.History.Application.SearchHistory;

/// <summary>
/// The single implementation of history filtering, shared by search and export so that an
/// export always selects exactly the rows the search endpoint would return for the same
/// parameters. Two copies of this logic is how the two endpoints would silently drift.
/// </summary>
public static class HistoryFilters
{
    /// <summary>
    /// Applies every history filter to the queryable. Pagination is deliberately not applied
    /// here: search pages, export does not.
    /// </summary>
    /// <param name="queryable">The base queryable to filter.</param>
    /// <param name="query">The search query containing filter criteria.</param>
    /// <returns>The filtered queryable.</returns>
    public static IQueryable<InferenceRecord> Apply(
        IQueryable<InferenceRecord> queryable, SearchHistoryQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.ToLowerInvariant();
            queryable = queryable.Where(r =>
                r.SourceModule.ToLower().Contains(search) ||
                r.Model.ToLower().Contains(search) ||
                r.RequestJson.ToLower().Contains(search) ||
                (r.ResponseJson != null && r.ResponseJson.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(query.SourceModule))
        {
            queryable = queryable.Where(r => r.SourceModule == query.SourceModule);
        }

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            queryable = queryable.Where(r => r.Model == query.Model);
        }

        // The date pickers send a bare "2026-08-09", which minimal-API binding turns into a
        // DateTime with Kind=Unspecified — and Npgsql refuses to compare that against a
        // `timestamp with time zone`, so every date filter returned a 500 rather than rows.
        // The column is UTC, so an unqualified date is read as a UTC one.
        if (query.From.HasValue)
        {
            DateTime from = DateTime.SpecifyKind(query.From.Value, DateTimeKind.Utc);
            queryable = queryable.Where(r => r.StartedAt >= from);
        }

        if (query.To.HasValue)
        {
            // Inclusive of the day chosen: a bare date binds to midnight at its start, so
            // "To = today" would otherwise exclude everything that happened today.
            DateTime to = DateTime.SpecifyKind(query.To.Value, DateTimeKind.Utc);
            DateTime toEndOfDay = to.TimeOfDay == TimeSpan.Zero ? to.AddDays(1).AddTicks(-1) : to;

            queryable = queryable.Where(r => r.StartedAt <= toEndOfDay);
        }

        // Tags is mapped as text[] (not jsonb) precisely so this predicate translates to
        // `@tag = ANY("Tags")` instead of throwing an untranslatable-expression error at
        // runtime — the failure documented as R8 in docs/features/history.md.
        if (query.Tags is { Count: > 0 })
        {
            foreach (string tag in query.Tags)
            {
                queryable = queryable.Where(r => r.Tags.Contains(tag));
            }
        }

        if (query.IsSuccess.HasValue)
        {
            queryable = queryable.Where(r => r.IsSuccess == query.IsSuccess.Value);
        }

        return queryable;
    }
}

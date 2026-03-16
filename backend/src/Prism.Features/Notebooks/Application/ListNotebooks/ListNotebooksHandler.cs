using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Notebooks.Application.Dtos;
using Prism.Features.Notebooks.Domain;

namespace Prism.Features.Notebooks.Application.ListNotebooks;

/// <summary>
/// Query to list notebooks with optional search.
/// </summary>
public sealed record ListNotebooksQuery(string? Search);

/// <summary>
/// Handles listing notebooks.
/// </summary>
public sealed class ListNotebooksHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListNotebooksHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ListNotebooksHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all notebooks, optionally filtered by search term.
    /// </summary>
    /// <param name="query">The list query parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A list of notebook summary DTOs.</returns>
    public async Task<Result<List<NotebookSummaryDto>>> HandleAsync(ListNotebooksQuery query, CancellationToken ct)
    {
        IQueryable<Notebook> queryable = _db.Set<Notebook>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.ToLower();
            queryable = queryable.Where(n =>
                n.Name.ToLower().Contains(search) ||
                (n.Description != null && n.Description.ToLower().Contains(search)));
        }

        List<NotebookSummaryDto> notebooks = await queryable
            .OrderByDescending(n => n.LastEditedAt ?? n.CreatedAt)
            .Select(n => NotebookSummaryDto.FromEntity(n))
            .ToListAsync(ct);

        return notebooks;
    }
}

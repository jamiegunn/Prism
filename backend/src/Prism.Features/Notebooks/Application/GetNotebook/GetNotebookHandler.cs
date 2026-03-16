using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Notebooks.Application.Dtos;
using Prism.Features.Notebooks.Domain;

namespace Prism.Features.Notebooks.Application.GetNotebook;

/// <summary>
/// Query to get a notebook by ID.
/// </summary>
public sealed record GetNotebookQuery(Guid Id);

/// <summary>
/// Handles retrieving a notebook with its full content.
/// </summary>
public sealed class GetNotebookHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetNotebookHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public GetNotebookHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets a notebook by ID including full .ipynb content.
    /// </summary>
    /// <param name="query">The get notebook query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The notebook detail DTO if found.</returns>
    public async Task<Result<NotebookDetailDto>> HandleAsync(GetNotebookQuery query, CancellationToken ct)
    {
        Notebook? notebook = await _db.Set<Notebook>()
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == query.Id, ct);

        if (notebook is null)
            return Error.NotFound($"Notebook {query.Id} not found.");

        return NotebookDetailDto.FromEntity(notebook);
    }
}

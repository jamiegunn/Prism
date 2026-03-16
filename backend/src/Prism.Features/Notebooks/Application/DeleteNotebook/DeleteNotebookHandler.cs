using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Notebooks.Domain;

namespace Prism.Features.Notebooks.Application.DeleteNotebook;

/// <summary>
/// Command to delete a notebook.
/// </summary>
public sealed record DeleteNotebookCommand(Guid Id);

/// <summary>
/// Handles deletion of notebooks.
/// </summary>
public sealed class DeleteNotebookHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteNotebookHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteNotebookHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DeleteNotebookHandler(AppDbContext db, ILogger<DeleteNotebookHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a notebook.
    /// </summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(DeleteNotebookCommand command, CancellationToken ct)
    {
        Notebook? notebook = await _db.Set<Notebook>()
            .FirstOrDefaultAsync(n => n.Id == command.Id, ct);

        if (notebook is null)
            return Error.NotFound($"Notebook {command.Id} not found.");

        _db.Set<Notebook>().Remove(notebook);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted notebook {NotebookId}", command.Id);

        return Result.Success();
    }
}

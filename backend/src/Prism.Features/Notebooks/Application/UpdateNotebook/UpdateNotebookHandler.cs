using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Notebooks.Application.Dtos;
using Prism.Features.Notebooks.Domain;

namespace Prism.Features.Notebooks.Application.UpdateNotebook;

/// <summary>
/// Command to update a notebook's content and/or metadata.
/// </summary>
public sealed record UpdateNotebookCommand(
    Guid Id,
    string? Name,
    string? Description,
    string? Content);

/// <summary>
/// Handles updating notebooks (save content, rename, etc.).
/// </summary>
public sealed class UpdateNotebookHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateNotebookHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNotebookHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public UpdateNotebookHandler(AppDbContext db, ILogger<UpdateNotebookHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Updates a notebook's content and metadata.
    /// </summary>
    /// <param name="command">The update notebook command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The updated notebook detail DTO.</returns>
    public async Task<Result<NotebookDetailDto>> HandleAsync(UpdateNotebookCommand command, CancellationToken ct)
    {
        Notebook? notebook = await _db.Set<Notebook>()
            .FirstOrDefaultAsync(n => n.Id == command.Id, ct);

        if (notebook is null)
            return Error.NotFound($"Notebook {command.Id} not found.");

        if (command.Name is not null)
            notebook.Name = command.Name;

        if (command.Description is not null)
            notebook.Description = command.Description;

        if (command.Content is not null)
        {
            notebook.Content = command.Content;
            notebook.SizeBytes = System.Text.Encoding.UTF8.GetByteCount(command.Content);
        }

        notebook.Version++;
        notebook.LastEditedAt = DateTime.UtcNow;
        notebook.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated notebook {NotebookId} to version {Version}", notebook.Id, notebook.Version);

        return NotebookDetailDto.FromEntity(notebook);
    }
}

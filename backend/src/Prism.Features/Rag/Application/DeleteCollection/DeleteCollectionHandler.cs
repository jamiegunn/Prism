using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.DeleteCollection;

/// <summary>
/// Command to delete a RAG collection and all its documents and chunks.
/// </summary>
public sealed record DeleteCollectionCommand(Guid Id);

/// <summary>
/// Handles deletion of a RAG collection.
/// </summary>
public sealed class DeleteCollectionHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteCollectionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteCollectionHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DeleteCollectionHandler(AppDbContext db, ILogger<DeleteCollectionHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a RAG collection and all related data.
    /// </summary>
    /// <param name="command">The deletion command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(DeleteCollectionCommand command, CancellationToken ct)
    {
        RagCollection? collection = await _db.Set<RagCollection>()
            .FirstOrDefaultAsync(c => c.Id == command.Id, ct);

        if (collection is null)
            return Error.NotFound($"RAG collection {command.Id} not found.");

        _db.Set<RagCollection>().Remove(collection);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted RAG collection {CollectionName} ({CollectionId})", collection.Name, collection.Id);

        return Result.Success();
    }
}

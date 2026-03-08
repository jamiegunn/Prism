using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.ListDocuments;

/// <summary>
/// Query to list documents in a RAG collection.
/// </summary>
public sealed record ListDocumentsQuery(Guid CollectionId);

/// <summary>
/// Handles listing documents for a collection.
/// </summary>
public sealed class ListDocumentsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListDocumentsHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ListDocumentsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists all documents in a collection.
    /// </summary>
    /// <param name="query">The query containing the collection ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of document DTOs.</returns>
    public async Task<Result<List<RagDocumentDto>>> HandleAsync(ListDocumentsQuery query, CancellationToken ct)
    {
        bool collectionExists = await _db.Set<RagCollection>()
            .AnyAsync(c => c.Id == query.CollectionId, ct);

        if (!collectionExists)
            return Error.NotFound($"RAG collection {query.CollectionId} not found.");

        List<RagDocument> documents = await _db.Set<RagDocument>()
            .AsNoTracking()
            .Where(d => d.CollectionId == query.CollectionId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return documents.Select(RagDocumentDto.FromEntity).ToList();
    }
}

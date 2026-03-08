using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.GetCollection;

/// <summary>
/// Query to get a specific RAG collection by ID.
/// </summary>
public sealed record GetCollectionQuery(Guid Id);

/// <summary>
/// Handles retrieval of a single RAG collection.
/// </summary>
public sealed class GetCollectionHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCollectionHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public GetCollectionHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Gets a RAG collection by ID.
    /// </summary>
    /// <param name="query">The query containing the collection ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the collection DTO or a not-found error.</returns>
    public async Task<Result<RagCollectionDto>> HandleAsync(GetCollectionQuery query, CancellationToken ct)
    {
        RagCollection? collection = await _db.Set<RagCollection>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);

        if (collection is null)
            return Error.NotFound($"RAG collection {query.Id} not found.");

        return RagCollectionDto.FromEntity(collection);
    }
}

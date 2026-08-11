using Microsoft.EntityFrameworkCore;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.QuerySets;

/// <summary>
/// Lists a collection's labelled query sets, and fetches one with its items.
/// </summary>
public sealed class ListQuerySetsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListQuerySetsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListQuerySetsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists the query sets of a collection, newest first.
    /// </summary>
    /// <param name="collectionId">The collection.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The sets, or NotFound for a missing collection.</returns>
    public async Task<Result<List<RagQuerySetDto>>> HandleAsync(Guid collectionId, CancellationToken ct)
    {
        bool collectionExists = await _db.Set<RagCollection>()
            .AnyAsync(c => c.Id == collectionId, ct);

        if (!collectionExists)
        {
            return Error.NotFound($"RAG collection {collectionId} not found.");
        }

        return await _db.Set<RagQuerySet>()
            .AsNoTracking()
            .Where(s => s.CollectionId == collectionId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new RagQuerySetDto(
                s.Id, s.CollectionId, s.Name, s.Description, s.Items.Count, s.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Fetches one query set with its items, in order.
    /// </summary>
    /// <param name="querySetId">The set.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The set with items, or NotFound.</returns>
    public async Task<Result<RagQuerySetDetailDto>> GetAsync(Guid querySetId, CancellationToken ct)
    {
        RagQuerySet? querySet = await _db.Set<RagQuerySet>()
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == querySetId, ct);

        if (querySet is null)
        {
            return Error.NotFound($"Query set {querySetId} not found.");
        }

        return new RagQuerySetDetailDto(
            querySet.Id,
            querySet.CollectionId,
            querySet.Name,
            querySet.Description,
            querySet.Items
                .OrderBy(i => i.OrderIndex)
                .Select(i => new RagQuerySetItemDto(i.Id, i.QueryText, i.RelevantChunkIds))
                .ToList());
    }
}

using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.GetCollectionStats;

/// <summary>
/// Query to get statistics for a RAG collection.
/// </summary>
public sealed record GetCollectionStatsQuery(Guid CollectionId);

/// <summary>
/// Statistics about a RAG collection.
/// </summary>
public sealed record CollectionStatsDto(
    Guid CollectionId,
    string Name,
    int DocumentCount,
    int ChunkCount,
    int TotalCharacters,
    int TotalTokens,
    double AverageChunkSize,
    Dictionary<string, int> DocumentsByStatus);

/// <summary>
/// Handles retrieval of collection statistics.
/// </summary>
public sealed class GetCollectionStatsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCollectionStatsHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public GetCollectionStatsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Gets statistics for a collection.
    /// </summary>
    /// <param name="query">The query containing the collection ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the collection statistics.</returns>
    public async Task<Result<CollectionStatsDto>> HandleAsync(GetCollectionStatsQuery query, CancellationToken ct)
    {
        RagCollection? collection = await _db.Set<RagCollection>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.CollectionId, ct);

        if (collection is null)
            return Error.NotFound($"RAG collection {query.CollectionId} not found.");

        List<RagDocument> documents = await _db.Set<RagDocument>()
            .AsNoTracking()
            .Where(d => d.CollectionId == query.CollectionId)
            .ToListAsync(ct);

        int totalChunks = await _db.Set<RagChunk>()
            .Where(c => documents.Select(d => d.Id).Contains(c.DocumentId))
            .CountAsync(ct);

        int totalTokens = await _db.Set<RagChunk>()
            .Where(c => documents.Select(d => d.Id).Contains(c.DocumentId))
            .SumAsync(c => c.TokenCount, ct);

        int totalCharacters = documents.Sum(d => d.CharacterCount);

        Dictionary<string, int> byStatus = documents
            .GroupBy(d => d.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        double avgChunkSize = totalChunks > 0 ? (double)totalCharacters / totalChunks : 0;

        return new CollectionStatsDto(
            collection.Id,
            collection.Name,
            documents.Count,
            totalChunks,
            totalCharacters,
            totalTokens,
            avgChunkSize,
            byStatus);
    }
}

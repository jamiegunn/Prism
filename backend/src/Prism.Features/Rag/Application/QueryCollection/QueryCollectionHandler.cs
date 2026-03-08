using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.QueryCollection;

/// <summary>
/// Query to search a RAG collection using vector, BM25, or hybrid search.
/// </summary>
public sealed record QueryCollectionQuery(
    Guid CollectionId,
    string QueryText,
    int TopK,
    SearchType SearchType,
    double VectorWeight = 0.7);

/// <summary>
/// Handles search queries against a RAG collection.
/// </summary>
public sealed class QueryCollectionHandler
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<QueryCollectionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCollectionHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="embeddingProvider">The embedding provider.</param>
    /// <param name="logger">The logger instance.</param>
    public QueryCollectionHandler(
        AppDbContext db,
        IEmbeddingProvider embeddingProvider,
        ILogger<QueryCollectionHandler> logger)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    /// <summary>
    /// Executes a search query against a collection.
    /// </summary>
    /// <param name="query">The search query parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the ranked search results.</returns>
    public async Task<Result<List<ChunkSearchResultDto>>> HandleAsync(QueryCollectionQuery query, CancellationToken ct)
    {
        RagCollection? collection = await _db.Set<RagCollection>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.CollectionId, ct);

        if (collection is null)
            return Error.NotFound($"RAG collection {query.CollectionId} not found.");

        int topK = query.TopK > 0 ? query.TopK : 5;

        return query.SearchType switch
        {
            SearchType.Vector => await VectorSearchAsync(collection, query.QueryText, topK, ct),
            SearchType.Bm25 => await Bm25SearchAsync(query.CollectionId, query.QueryText, topK, ct),
            SearchType.Hybrid => await HybridSearchAsync(collection, query.QueryText, topK, query.VectorWeight, ct),
            _ => await VectorSearchAsync(collection, query.QueryText, topK, ct)
        };
    }

    private async Task<Result<List<ChunkSearchResultDto>>> VectorSearchAsync(
        RagCollection collection, string queryText, int topK, CancellationToken ct)
    {
        Result<float[]> embedResult = await _embeddingProvider.EmbedAsync(queryText, collection.EmbeddingModel, ct);
        if (embedResult.IsFailure)
            return Result<List<ChunkSearchResultDto>>.Failure(embedResult.Error);

        var queryVector = new Vector(embedResult.Value);

        // Use pgvector cosine distance ordering
        List<ChunkSearchResultDto> results = await _db.Set<RagChunk>()
            .AsNoTracking()
            .Include("") // No include needed, we'll join manually
            .Where(c => _db.Set<RagDocument>()
                .Where(d => d.CollectionId == collection.Id)
                .Select(d => d.Id)
                .Contains(c.DocumentId))
            .Where(c => c.Embedding != null)
            .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
            .Take(topK)
            .Select(c => new ChunkSearchResultDto(
                c.Id,
                c.DocumentId,
                _db.Set<RagDocument>().Where(d => d.Id == c.DocumentId).Select(d => d.Filename).FirstOrDefault() ?? "",
                c.Content,
                1.0 - c.Embedding!.CosineDistance(queryVector),
                c.OrderIndex,
                c.TokenCount,
                c.Metadata))
            .ToListAsync(ct);

        return results;
    }

    private async Task<Result<List<ChunkSearchResultDto>>> Bm25SearchAsync(
        Guid collectionId, string queryText, int topK, CancellationToken ct)
    {
        // Use raw SQL for tsvector search since EF Core doesn't natively support ts_rank
        string sql = @"
            SELECT c.""Id"", c.""DocumentId"", d.""Filename"", c.""Content"",
                   ts_rank(c.search_vector, plainto_tsquery('english', {0}))::double precision AS ""Score"",
                   c.""OrderIndex"", c.""TokenCount"", c.""Metadata""
            FROM rag_chunks c
            JOIN rag_documents d ON d.""Id"" = c.""DocumentId""
            WHERE d.""CollectionId"" = {1}
              AND c.search_vector @@ plainto_tsquery('english', {0})
            ORDER BY ""Score"" DESC
            LIMIT {2}";

        List<ChunkSearchResultDto> results = await _db.Database
            .SqlQueryRaw<Bm25Result>(sql, queryText, collectionId, topK)
            .Select(r => new ChunkSearchResultDto(
                r.Id, r.DocumentId, r.Filename, r.Content,
                r.Score, r.OrderIndex, r.TokenCount, new Dictionary<string, string>()))
            .ToListAsync(ct);

        return results;
    }

    private async Task<Result<List<ChunkSearchResultDto>>> HybridSearchAsync(
        RagCollection collection, string queryText, int topK, double vectorWeight, CancellationToken ct)
    {
        // Fetch more candidates from each method, then merge
        int candidateCount = topK * 3;

        Result<List<ChunkSearchResultDto>> vectorResult = await VectorSearchAsync(collection, queryText, candidateCount, ct);
        if (vectorResult.IsFailure)
            return vectorResult;

        Result<List<ChunkSearchResultDto>> bm25Result = await Bm25SearchAsync(collection.Id, queryText, candidateCount, ct);
        // BM25 may fail if no tsvector matches — that's OK, we still have vector results
        List<ChunkSearchResultDto> bm25Results = bm25Result.IsSuccess ? bm25Result.Value : [];

        // Normalize scores within each result set
        List<ChunkSearchResultDto> vectorResults = vectorResult.Value;

        double vectorMax = vectorResults.Count > 0 ? vectorResults.Max(r => r.Score) : 1.0;
        double bm25Max = bm25Results.Count > 0 ? bm25Results.Max(r => r.Score) : 1.0;

        double bm25Weight = 1.0 - vectorWeight;

        // Merge by chunk ID with weighted scores
        var scoreMap = new Dictionary<Guid, (ChunkSearchResultDto Result, double Score)>();

        foreach (ChunkSearchResultDto r in vectorResults)
        {
            double normalizedScore = vectorMax > 0 ? r.Score / vectorMax : 0;
            scoreMap[r.ChunkId] = (r, normalizedScore * vectorWeight);
        }

        foreach (ChunkSearchResultDto r in bm25Results)
        {
            double normalizedScore = bm25Max > 0 ? r.Score / bm25Max : 0;
            if (scoreMap.TryGetValue(r.ChunkId, out var existing))
            {
                scoreMap[r.ChunkId] = (existing.Result, existing.Score + normalizedScore * bm25Weight);
            }
            else
            {
                scoreMap[r.ChunkId] = (r, normalizedScore * bm25Weight);
            }
        }

        List<ChunkSearchResultDto> hybridResults = scoreMap.Values
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Result with { Score = x.Score })
            .ToList();

        return hybridResults;
    }

    private sealed record Bm25Result(
        Guid Id,
        Guid DocumentId,
        string Filename,
        string Content,
        double Score,
        int OrderIndex,
        int TokenCount,
        string Metadata);
}

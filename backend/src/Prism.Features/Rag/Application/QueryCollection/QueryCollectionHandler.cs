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
    public async Task<Result<ChunkSearchOutcomeDto>> HandleAsync(QueryCollectionQuery query, CancellationToken ct)
    {
        // An empty query is the caller's mistake, caught here rather than paid for by an
        // embedding round trip that fails with a provider 503 — which read as "the server is
        // down" for what is really "you asked for nothing".
        if (string.IsNullOrWhiteSpace(query.QueryText))
            return Error.Validation("A search query is required.");

        RagCollection? collection = await _db.Set<RagCollection>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.CollectionId, ct);

        if (collection is null)
            return Error.NotFound($"RAG collection {query.CollectionId} not found.");

        int topK = query.TopK > 0 ? query.TopK : 5;

        return query.SearchType switch
        {
            SearchType.Bm25 => Wrap(await Bm25SearchAsync(query.CollectionId, query.QueryText, topK, ct)),
            SearchType.Hybrid => await HybridSearchAsync(collection, query.QueryText, topK, query.VectorWeight, ct),
            _ => Wrap(await VectorSearchAsync(collection, query.QueryText, topK, ct)),
        };
    }

    /// <summary>
    /// Presents a single-method search as an outcome, which by definition ran as asked.
    /// </summary>
    /// <param name="result">The search result.</param>
    /// <returns>The wrapped outcome, or the original failure.</returns>
    private static Result<ChunkSearchOutcomeDto> Wrap(Result<List<ChunkSearchResultDto>> result)
        => result.IsFailure
            ? Result<ChunkSearchOutcomeDto>.Failure(result.Error)
            : ChunkSearchOutcomeDto.Complete(result.Value);

    private async Task<Result<List<ChunkSearchResultDto>>> VectorSearchAsync(
        RagCollection collection, string queryText, int topK, CancellationToken ct)
    {
        Result<float[]> embedResult = await _embeddingProvider.EmbedAsync(queryText, collection.EmbeddingModel, ct);
        if (embedResult.IsFailure)
            return Result<List<ChunkSearchResultDto>>.Failure(embedResult.Error);

        var queryVector = new Vector(embedResult.Value);

        IQueryable<RagChunk> embedded = _db.Set<RagChunk>()
            .AsNoTracking()
            .Where(c => _db.Set<RagDocument>()
                .Where(d => d.CollectionId == collection.Id)
                .Select(d => d.Id)
                .Contains(c.DocumentId))
            .Where(c => c.Embedding != null);

        // The metric a collection was created with is the metric it is searched by. All three
        // were offered, stored, and then ignored — every search ranked by cosine, so a collection
        // built to compare metrics compared nothing and said nothing about it.
        //
        // Ranking stays in the database, where the vector index is. Only the operator differs,
        // and all three of pgvector's are "smaller is better" — cosine distance, L2 distance, and
        // the negated inner product — so each branch is the same query with its own ordering.
        IQueryable<RagChunk> ranked = collection.DistanceMetric switch
        {
            DistanceMetricType.Euclidean =>
                embedded.OrderBy(c => c.Embedding!.L2Distance(queryVector)),

            DistanceMetricType.InnerProduct =>
                embedded.OrderBy(c => c.Embedding!.MaxInnerProduct(queryVector)),

            _ => embedded.OrderBy(c => c.Embedding!.CosineDistance(queryVector)),
        };

        List<RagChunk> matches = await ranked.Take(topK).ToListAsync(ct);

        // One lookup for the page of results rather than a correlated subquery per row.
        List<Guid> documentIds = [.. matches.Select(c => c.DocumentId).Distinct()];

        Dictionary<Guid, string> filenames = await _db.Set<RagDocument>()
            .AsNoTracking()
            .Where(d => documentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Filename, ct);

        // The score is computed here rather than asked for a second time in SQL. The rows are
        // already in hand with their vectors, and the alternative — repeating each distance
        // expression inside the projection — is a second operator call per row for a number the
        // database has already worked out once.
        float[] query = embedResult.Value;

        return matches
            .Select(c => new ChunkSearchResultDto(
                c.Id,
                c.DocumentId,
                filenames.GetValueOrDefault(c.DocumentId, ""),
                c.Content,
                Similarity(collection.DistanceMetric, c.Embedding!.ToArray(), query),
                c.OrderIndex,
                c.TokenCount,
                c.Metadata))
            .ToList();
    }

    /// <summary>
    /// Turns a metric's distance into a score where larger is better.
    /// </summary>
    /// <param name="metric">The collection's metric.</param>
    /// <param name="chunk">The chunk's embedding.</param>
    /// <param name="query">The query embedding.</param>
    /// <returns>The similarity to report, on a scale where larger is better.</returns>
    /// <remarks>
    /// Each metric measures a different thing, so each needs its own answer, and the numbers are
    /// only comparable within one metric. Euclidean's is a distance where smaller is better;
    /// reported raw it would leave a list ordered best-first with its scores climbing, so it
    /// becomes <c>1 / (1 + d)</c> — bounded, and falling as distance grows.
    /// </remarks>
    internal static double Similarity(DistanceMetricType metric, float[] chunk, float[] query)
    {
        int length = Math.Min(chunk.Length, query.Length);
        double dot = 0, chunkSquared = 0, querySquared = 0, gaps = 0;

        for (int i = 0; i < length; i++)
        {
            double a = chunk[i], b = query[i];
            dot += a * b;
            chunkSquared += a * a;
            querySquared += b * b;
            gaps += (a - b) * (a - b);
        }

        return metric switch
        {
            DistanceMetricType.Euclidean => 1.0 / (1.0 + Math.Sqrt(gaps)),
            DistanceMetricType.InnerProduct => dot,

            // Two vectors cannot be at any angle to each other if one of them has no direction.
            _ => chunkSquared > 0 && querySquared > 0
                ? dot / (Math.Sqrt(chunkSquared) * Math.Sqrt(querySquared))
                : 0.0,
        };
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

    private async Task<Result<ChunkSearchOutcomeDto>> HybridSearchAsync(
        RagCollection collection, string queryText, int topK, double vectorWeight, CancellationToken ct)
    {
        // Fetch more candidates from each method, then merge
        int candidateCount = topK * 3;

        Result<List<ChunkSearchResultDto>> vectorResult = await VectorSearchAsync(collection, queryText, candidateCount, ct);

        Result<List<ChunkSearchResultDto>> bm25Result = await Bm25SearchAsync(collection.Id, queryText, candidateCount, ct);
        // BM25 may fail if no tsvector matches — that's OK, we still have vector results
        List<ChunkSearchResultDto> bm25Results = bm25Result.IsSuccess ? bm25Result.Value : [];

        // A failed vector half used to fail the whole search, throwing away the keyword half
        // already computed — which is how "no embedding server" became "no results at all" on a
        // page whose other mode was working fine. The keyword half is returned instead, labelled,
        // because results from half a hybrid search are useful but are not a hybrid search, and
        // the difference is exactly what a retrieval comparison is about.
        if (vectorResult.IsFailure)
        {
            _logger.LogWarning(
                "Hybrid search on {CollectionId} fell back to BM25: {Reason}",
                collection.Id, vectorResult.Error.Message);

            return new ChunkSearchOutcomeDto(
                bm25Results.Take(topK).ToList(),
                $"The vector half could not run ({vectorResult.Error.Message}), so these are " +
                "BM25 keyword results only, not hybrid.");
        }

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

        return ChunkSearchOutcomeDto.Complete(hybridResults);
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

using Microsoft.EntityFrameworkCore;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.QuerySets;

/// <summary>
/// Command to evaluate a collection's retrieval modes against a labelled query set.
/// </summary>
/// <param name="CollectionId">The collection.</param>
/// <param name="QuerySetId">The labelled set to score against.</param>
/// <param name="TopK">The ranking depth to retrieve per query.</param>
/// <param name="Modes">The modes to evaluate; null means all three.</param>
public sealed record EvaluateRetrievalCommand(
    Guid CollectionId,
    Guid QuerySetId,
    int TopK,
    List<SearchType>? Modes);

/// <summary>
/// Runs each retrieval mode over every labelled query and scores the rankings with
/// precision@k, recall@k, MRR and nDCG@k — so vector, BM25 and hybrid are compared on
/// evidence rather than by eye.
/// </summary>
/// <remarks>
/// The metrics are comparable across modes because they are computed from ranks against the
/// same labels. The raw scores the modes return are <em>not</em> comparable — hybrid scores
/// are max-normalized and blended — which is why this evaluation reports rank metrics and
/// never averages raw scores across modes.
/// </remarks>
public sealed class EvaluateRetrievalHandler
{
    /// <summary>The cutoffs metrics are reported at, capped at the requested top-k.</summary>
    private static readonly int[] Cutoffs = [1, 3, 5, 10];

    private readonly AppDbContext _db;
    private readonly QueryCollectionHandler _queryHandler;
    private readonly ILogger<EvaluateRetrievalHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluateRetrievalHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="queryHandler">The search handler the app itself uses — the evaluation
    /// must score the retrieval users get, not a parallel implementation.</param>
    /// <param name="logger">The logger instance.</param>
    public EvaluateRetrievalHandler(
        AppDbContext db,
        QueryCollectionHandler queryHandler,
        ILogger<EvaluateRetrievalHandler> logger)
    {
        _db = db;
        _queryHandler = queryHandler;
        _logger = logger;
    }

    /// <summary>
    /// Handles the evaluation.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>Per-mode metrics, with per-mode failure stated rather than zeroed.</returns>
    public async Task<Result<RetrievalEvaluationDto>> HandleAsync(
        EvaluateRetrievalCommand command, CancellationToken ct)
    {
        RagQuerySet? querySet = await _db.Set<RagQuerySet>()
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.QuerySetId, ct);

        if (querySet is null)
        {
            return Error.NotFound($"Query set {command.QuerySetId} not found.");
        }

        if (querySet.CollectionId != command.CollectionId)
        {
            return Error.Validation(
                "That query set labels a different collection; its chunk ids mean nothing here.");
        }

        if (querySet.Items.Count == 0)
        {
            return Error.Validation("The query set has no labelled queries.");
        }

        int topK = command.TopK > 0 ? command.TopK : 10;
        List<SearchType> modes = command.Modes is { Count: > 0 }
            ? command.Modes
            : [SearchType.Vector, SearchType.Bm25, SearchType.Hybrid];

        var modeResults = new List<RetrievalModeResultDto>();

        foreach (SearchType mode in modes)
        {
            modeResults.Add(await EvaluateModeAsync(command.CollectionId, querySet, mode, topK, ct));
        }

        return new RetrievalEvaluationDto(
            command.CollectionId,
            command.QuerySetId,
            topK,
            modeResults,
            BuildDefinitions(topK));
    }

    private async Task<RetrievalModeResultDto> EvaluateModeAsync(
        Guid collectionId, RagQuerySet querySet, SearchType mode, int topK, CancellationToken ct)
    {
        string modeName = mode.ToString().ToLowerInvariant();

        // Vector search over a collection with no embedded chunks succeeds with an empty
        // ranking, which would score as "retrieves nothing relevant". The truth is "there is
        // nothing to rank" — the seeded collection ships exactly this way — so it is
        // reported as the mode being unavailable, with the fix stated.
        if (mode is SearchType.Vector or SearchType.Hybrid)
        {
            bool anyEmbedded = await _db.Set<RagChunk>()
                .Join(
                    _db.Set<RagDocument>().Where(d => d.CollectionId == collectionId),
                    chunk => chunk.DocumentId,
                    doc => doc.Id,
                    (chunk, _) => chunk)
                .AnyAsync(c => c.Embedding != null, ct);

            if (!anyEmbedded)
            {
                return new RetrievalModeResultDto(
                    modeName,
                    0,
                    null,
                    "No chunk in this collection has an embedding, so vector ranking has " +
                    "nothing to rank. Re-ingest the documents with an embedding provider " +
                    "available to evaluate this mode.");
            }
        }

        var perQueryMetrics = new List<Dictionary<string, double>>();

        foreach (RagQuerySetItem item in querySet.Items.OrderBy(i => i.OrderIndex))
        {
            Result<List<ChunkSearchResultDto>> retrieval = await _queryHandler.HandleAsync(
                new QueryCollectionQuery(collectionId, item.QueryText, topK, mode), ct);

            if (retrieval.IsFailure)
            {
                // A mode that cannot run has no metrics. Reporting zeros here would claim
                // "this mode retrieves nothing relevant", which is a different statement
                // from "this mode could not run" — for vector search on a collection with
                // null embeddings, the second is the truth.
                _logger.LogWarning(
                    "Retrieval evaluation: {Mode} failed on collection {CollectionId}: {Error}",
                    modeName, collectionId, retrieval.Error.Message);

                return new RetrievalModeResultDto(
                    modeName, 0, null, retrieval.Error.Message);
            }

            List<Guid> ranked = retrieval.Value.Select(r => r.ChunkId).ToList();
            var relevant = item.RelevantChunkIds.ToHashSet();

            var metrics = new Dictionary<string, double>();

            foreach (int k in Cutoffs.Where(k => k <= topK))
            {
                metrics[$"precision@{k}"] = RetrievalMetrics.PrecisionAtK(ranked, relevant, k);

                double? recall = RetrievalMetrics.RecallAtK(ranked, relevant, k);
                if (recall is not null)
                {
                    metrics[$"recall@{k}"] = recall.Value;
                }
            }

            metrics["mrr"] = RetrievalMetrics.ReciprocalRank(ranked, relevant);

            double? ndcg = RetrievalMetrics.NdcgAtK(ranked, relevant, topK);
            if (ndcg is not null)
            {
                metrics[$"ndcg@{topK}"] = ndcg.Value;
            }

            perQueryMetrics.Add(metrics);
        }

        // Mean over queries, per metric key. Keys missing for a query (undefined metric) are
        // excluded from that metric's mean rather than counted as zero.
        Dictionary<string, double> means = perQueryMetrics
            .SelectMany(m => m.Keys)
            .Distinct()
            .ToDictionary(
                key => key,
                key => perQueryMetrics
                    .Where(m => m.ContainsKey(key))
                    .Average(m => m[key]));

        return new RetrievalModeResultDto(modeName, perQueryMetrics.Count, means, null);
    }

    private static Dictionary<string, string> BuildDefinitions(int topK) => new()
    {
        ["precision@k"] =
            "Relevant items in the top k, divided by k (by k even when fewer were " +
            "retrieved). Mean over queries.",
        ["recall@k"] =
            "Relevant items in the top k, divided by the number of relevant items. " +
            "Mean over queries; undefined (excluded) for a query with no relevant items.",
        ["mrr"] =
            "Mean reciprocal rank: 1/rank of the first relevant item in the returned " +
            $"ranking (depth {topK}), 0 when none appears. Mean over queries.",
        [$"ndcg@{topK}"] =
            "Normalized discounted cumulative gain, binary relevance, gain 1/log2(rank+1), " +
            $"ideal = min(k, |relevant|) relevant items on top, k = {topK}. Mean over queries.",
        ["note"] =
            "Rank metrics are comparable across modes; the modes' raw scores are not " +
            "(hybrid scores are normalized and blended).",
    };
}

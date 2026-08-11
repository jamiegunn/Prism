namespace Prism.Features.Rag.Domain;

/// <summary>
/// The standard ranked-retrieval metrics — precision@k, recall@k, MRR and nDCG@k with binary
/// relevance — computed exactly as their definitions state. Proved by hand-worked examples
/// and invariants in <c>RetrievalMetricsTests</c>, including the truncation cases nDCG is
/// usually got wrong on.
/// </summary>
/// <remarks>
/// Definitions used, stated here because each has variants in the literature:
/// precision@k divides by <c>k</c> even when fewer than <c>k</c> items were retrieved (an
/// empty tail is a miss, not a discount); recall@k divides by the number of relevant items;
/// MRR is the reciprocal rank of the first relevant item anywhere in the returned ranking, 0
/// when none appears; DCG@k uses gain <c>1/log2(i+1)</c> at 1-based rank <c>i</c>, and
/// nDCG@k normalizes by the ideal DCG@k of <c>min(k, |relevant|)</c> ones at the top.
/// A duplicate chunk id in the ranking counts once, at its first (best) rank.
/// </remarks>
public static class RetrievalMetrics
{
    /// <summary>
    /// Precision at k: the fraction of the top-k slots holding a relevant item.
    /// </summary>
    /// <param name="ranked">The ranked chunk ids, best first.</param>
    /// <param name="relevant">The relevant chunk ids.</param>
    /// <param name="k">The cutoff, ≥ 1.</param>
    /// <returns>Precision in [0, 1].</returns>
    public static double PrecisionAtK(
        IReadOnlyList<Guid> ranked, IReadOnlySet<Guid> relevant, int k)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);

        return (double)RelevantInTopK(ranked, relevant, k) / k;
    }

    /// <summary>
    /// Recall at k: the fraction of relevant items found in the top k. Null when there are
    /// no relevant items — recall of nothing is undefined, not 1 and not 0.
    /// </summary>
    /// <param name="ranked">The ranked chunk ids, best first.</param>
    /// <param name="relevant">The relevant chunk ids.</param>
    /// <param name="k">The cutoff, ≥ 1.</param>
    /// <returns>Recall in [0, 1], or null with an empty relevant set.</returns>
    public static double? RecallAtK(
        IReadOnlyList<Guid> ranked, IReadOnlySet<Guid> relevant, int k)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);

        if (relevant.Count == 0)
        {
            return null;
        }

        return (double)RelevantInTopK(ranked, relevant, k) / relevant.Count;
    }

    /// <summary>
    /// Mean reciprocal rank contribution of one query: 1/rank of the first relevant item in
    /// the ranking, or 0 when no relevant item appears at all.
    /// </summary>
    /// <param name="ranked">The ranked chunk ids, best first.</param>
    /// <param name="relevant">The relevant chunk ids.</param>
    /// <returns>The reciprocal rank in [0, 1].</returns>
    public static double ReciprocalRank(IReadOnlyList<Guid> ranked, IReadOnlySet<Guid> relevant)
    {
        var seen = new HashSet<Guid>();

        for (int i = 0; i < ranked.Count; i++)
        {
            if (!seen.Add(ranked[i]))
            {
                continue;
            }

            if (relevant.Contains(ranked[i]))
            {
                return 1.0 / (i + 1);
            }
        }

        return 0.0;
    }

    /// <summary>
    /// Normalized discounted cumulative gain at k with binary relevance. Null when there are
    /// no relevant items (the ideal DCG is 0 and the ratio is undefined).
    /// </summary>
    /// <param name="ranked">The ranked chunk ids, best first.</param>
    /// <param name="relevant">The relevant chunk ids.</param>
    /// <param name="k">The cutoff, ≥ 1.</param>
    /// <returns>nDCG in [0, 1], or null with an empty relevant set.</returns>
    public static double? NdcgAtK(
        IReadOnlyList<Guid> ranked, IReadOnlySet<Guid> relevant, int k)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);

        if (relevant.Count == 0)
        {
            return null;
        }

        double dcg = 0.0;
        var seen = new HashSet<Guid>();
        int rank = 0;

        foreach (Guid id in ranked)
        {
            if (!seen.Add(id))
            {
                continue; // duplicate: counted once at its best rank
            }

            rank++;

            if (rank > k)
            {
                break;
            }

            if (relevant.Contains(id))
            {
                dcg += 1.0 / Math.Log2(rank + 1);
            }
        }

        // Ideal ranking: min(k, |relevant|) relevant items in the top positions. The
        // truncation matters both ways: with more relevant items than k, the ideal can only
        // fit k of them; with fewer, ranks beyond |relevant| contribute nothing.
        int idealCount = Math.Min(k, relevant.Count);
        double idealDcg = 0.0;

        for (int i = 1; i <= idealCount; i++)
        {
            idealDcg += 1.0 / Math.Log2(i + 1);
        }

        return dcg / idealDcg;
    }

    private static int RelevantInTopK(
        IReadOnlyList<Guid> ranked, IReadOnlySet<Guid> relevant, int k)
    {
        var seen = new HashSet<Guid>();
        int hits = 0;
        int rank = 0;

        foreach (Guid id in ranked)
        {
            if (!seen.Add(id))
            {
                continue;
            }

            rank++;

            if (rank > k)
            {
                break;
            }

            if (relevant.Contains(id))
            {
                hits++;
            }
        }

        return hits;
    }
}

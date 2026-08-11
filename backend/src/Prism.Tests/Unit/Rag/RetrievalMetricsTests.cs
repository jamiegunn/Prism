using Prism.Features.Rag.Domain;

namespace Prism.Tests.Unit.Rag;

/// <summary>
/// Proofs for the retrieval metrics: worked examples a reviewer checks by hand — including
/// the truncation cases nDCG is usually got wrong on — and invariants over generated
/// rankings.
/// </summary>
public sealed class RetrievalMetricsTests
{
    private const double Tolerance = 1e-12;

    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = Guid.Parse("00000000-0000-0000-0000-00000000000d");
    private static readonly Guid E = Guid.Parse("00000000-0000-0000-0000-00000000000e");

    /// <summary>
    /// Worked example. Ranking [A, B, C, D, E], relevant {A, C, E}:
    /// precision@1 = 1/1; precision@3 = 2/3 (A, C in top 3); precision@5 = 3/5.
    /// recall@1 = 1/3; recall@3 = 2/3; recall@5 = 3/3.
    /// MRR = 1 (first item relevant).
    /// DCG@5 = 1/log2(2) + 1/log2(4) + 1/log2(6) = 1 + 0.5 + 1/log2(6).
    /// IDCG@5 (3 relevant) = 1/log2(2) + 1/log2(3) + 1/log2(4) = 1 + 1/log2(3) + 0.5.
    /// </summary>
    [Fact]
    public void Worked_Example_Mixed_Ranking()
    {
        Guid[] ranked = [A, B, C, D, E];
        HashSet<Guid> relevant = [A, C, E];

        Assert.Equal(1.0, RetrievalMetrics.PrecisionAtK(ranked, relevant, 1), Tolerance);
        Assert.Equal(2.0 / 3.0, RetrievalMetrics.PrecisionAtK(ranked, relevant, 3), Tolerance);
        Assert.Equal(3.0 / 5.0, RetrievalMetrics.PrecisionAtK(ranked, relevant, 5), Tolerance);

        Assert.Equal(1.0 / 3.0, RetrievalMetrics.RecallAtK(ranked, relevant, 1)!.Value, Tolerance);
        Assert.Equal(2.0 / 3.0, RetrievalMetrics.RecallAtK(ranked, relevant, 3)!.Value, Tolerance);
        Assert.Equal(1.0, RetrievalMetrics.RecallAtK(ranked, relevant, 5)!.Value, Tolerance);

        Assert.Equal(1.0, RetrievalMetrics.ReciprocalRank(ranked, relevant), Tolerance);

        double dcg = 1.0 / Math.Log2(2) + 1.0 / Math.Log2(4) + 1.0 / Math.Log2(6);
        double idcg = 1.0 / Math.Log2(2) + 1.0 / Math.Log2(3) + 1.0 / Math.Log2(4);
        Assert.Equal(dcg / idcg, RetrievalMetrics.NdcgAtK(ranked, relevant, 5)!.Value, Tolerance);
    }

    /// <summary>
    /// Truncation case 1: more relevant items than k. Ranking [A, B], relevant {A, B, C, D}:
    /// the ideal@2 can only hold 2 of the 4 relevant items, so IDCG@2 = 1 + 1/log2(3) — not
    /// the sum over all four. Here the ranking IS ideal at depth 2, so nDCG@2 must be
    /// exactly 1, which an implementation normalizing by all |relevant| gets wrong (0.56…).
    /// </summary>
    [Fact]
    public void Ndcg_Ideal_Truncates_At_K_When_Relevant_Exceeds_K()
    {
        Guid[] ranked = [A, B];
        HashSet<Guid> relevant = [A, B, C, D];

        Assert.Equal(1.0, RetrievalMetrics.NdcgAtK(ranked, relevant, 2)!.Value, Tolerance);
    }

    /// <summary>
    /// Truncation case 2: fewer relevant items than k. Relevant {A} found at rank 3 of 5:
    /// DCG@5 = 1/log2(4) = 0.5; IDCG@5 = 1/log2(2) = 1 (one relevant item, not five ranks of
    /// gain). nDCG@5 = 0.5 exactly.
    /// </summary>
    [Fact]
    public void Ndcg_Ideal_Truncates_At_Relevant_Count_When_Fewer_Than_K()
    {
        Guid[] ranked = [B, C, A, D, E];
        HashSet<Guid> relevant = [A];

        Assert.Equal(0.5, RetrievalMetrics.NdcgAtK(ranked, relevant, 5)!.Value, Tolerance);
    }

    /// <summary>
    /// Tie/duplicate case: a chunk id appearing twice in a ranking counts once, at its best
    /// rank. Ranking [A, A, B] with relevant {A, B}: the duplicate A does not double-count
    /// (precision@2 would otherwise be 1), and B is at effective rank 2, so precision@2 = 1,
    /// recall@2 = 1, nDCG@2 = 1.
    /// </summary>
    [Fact]
    public void Duplicate_Ids_Count_Once_At_Their_Best_Rank()
    {
        Guid[] ranked = [A, A, B];
        HashSet<Guid> relevant = [A, B];

        Assert.Equal(1.0, RetrievalMetrics.PrecisionAtK(ranked, relevant, 2), Tolerance);
        Assert.Equal(1.0, RetrievalMetrics.RecallAtK(ranked, relevant, 2)!.Value, Tolerance);
        Assert.Equal(1.0, RetrievalMetrics.NdcgAtK(ranked, relevant, 2)!.Value, Tolerance);
    }

    /// <summary>
    /// Short-ranking case: k larger than the ranking. Ranking [A] (one item, relevant),
    /// k = 5: precision@5 = 1/5 — the empty tail is missed retrieval, not a discount.
    /// Recall@5 = 1/1. MRR = 1.
    /// </summary>
    [Fact]
    public void Precision_Divides_By_K_Even_When_Fewer_Items_Were_Retrieved()
    {
        Guid[] ranked = [A];
        HashSet<Guid> relevant = [A];

        Assert.Equal(0.2, RetrievalMetrics.PrecisionAtK(ranked, relevant, 5), Tolerance);
        Assert.Equal(1.0, RetrievalMetrics.RecallAtK(ranked, relevant, 5)!.Value, Tolerance);
        Assert.Equal(1.0, RetrievalMetrics.ReciprocalRank(ranked, relevant), Tolerance);
    }

    /// <summary>
    /// Degenerate cases: empty ranking → precision 0, recall 0, MRR 0, nDCG 0; empty
    /// relevant set → recall and nDCG are null (undefined), precision 0, MRR 0. Null is the
    /// point: recall of nothing is not a number, and returning 0 or 1 would poison averages.
    /// </summary>
    [Fact]
    public void Degenerate_Inputs()
    {
        HashSet<Guid> relevant = [A];
        Guid[] empty = [];

        Assert.Equal(0.0, RetrievalMetrics.PrecisionAtK(empty, relevant, 3), Tolerance);
        Assert.Equal(0.0, RetrievalMetrics.RecallAtK(empty, relevant, 3)!.Value, Tolerance);
        Assert.Equal(0.0, RetrievalMetrics.ReciprocalRank(empty, relevant), Tolerance);
        Assert.Equal(0.0, RetrievalMetrics.NdcgAtK(empty, relevant, 3)!.Value, Tolerance);

        Guid[] ranked = [A, B];
        HashSet<Guid> nothingRelevant = [];

        Assert.Equal(0.0, RetrievalMetrics.PrecisionAtK(ranked, nothingRelevant, 2), Tolerance);
        Assert.Null(RetrievalMetrics.RecallAtK(ranked, nothingRelevant, 2));
        Assert.Equal(0.0, RetrievalMetrics.ReciprocalRank(ranked, nothingRelevant), Tolerance);
        Assert.Null(RetrievalMetrics.NdcgAtK(ranked, nothingRelevant, 2));
    }

    /// <summary>
    /// MRR positions: relevant item at rank r gives exactly 1/r; no relevant item gives 0.
    /// </summary>
    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 0.5)]
    [InlineData(3, 1.0 / 3.0)]
    [InlineData(5, 0.2)]
    public void Reciprocal_Rank_Is_One_Over_The_First_Relevant_Rank(int position, double expected)
    {
        Guid[] pool = [A, B, C, D, E];
        Guid[] ranked = pool.ToArray();
        HashSet<Guid> relevant = [pool[position - 1]];

        Assert.Equal(expected, RetrievalMetrics.ReciprocalRank(ranked, relevant), Tolerance);
    }

    /// <summary>
    /// Invariants over generated rankings: recall@k is non-decreasing in k; a perfect
    /// ranking (all relevant items first) has precision@k exactly 1 for every k up to the
    /// number of relevant items; the ideal ranking's nDCG is exactly 1 at every depth; and
    /// every metric stays within [0, 1].
    /// </summary>
    [Fact]
    public void Invariants_Hold_Across_Generated_Rankings()
    {
        var random = new Random(20260811);

        for (int trial = 0; trial < 200; trial++)
        {
            int poolSize = random.Next(1, 30);
            Guid[] pool = Enumerable.Range(0, poolSize).Select(_ => Guid.NewGuid()).ToArray();

            Guid[] ranked = pool.OrderBy(_ => random.Next()).ToArray();
            HashSet<Guid> relevant = pool.Where(_ => random.Next(3) == 0).ToHashSet();

            if (relevant.Count == 0)
            {
                relevant.Add(pool[random.Next(poolSize)]);
            }

            double previousRecall = 0.0;

            for (int k = 1; k <= poolSize + 2; k++)
            {
                double precision = RetrievalMetrics.PrecisionAtK(ranked, relevant, k);
                double recall = RetrievalMetrics.RecallAtK(ranked, relevant, k)!.Value;
                double? ndcg = RetrievalMetrics.NdcgAtK(ranked, relevant, k);

                Assert.InRange(precision, 0.0, 1.0);
                Assert.InRange(recall, 0.0, 1.0);
                Assert.InRange(ndcg!.Value, 0.0, 1.0 + 1e-12);

                // Recall@k is non-decreasing in k.
                Assert.True(recall >= previousRecall - Tolerance,
                    $"recall@{k} ({recall}) < recall@{k - 1} ({previousRecall})");
                previousRecall = recall;
            }

            // The ideal ranking: all relevant items first.
            Guid[] ideal = ranked.OrderByDescending(id => relevant.Contains(id)).ToArray();

            for (int k = 1; k <= poolSize; k++)
            {
                Assert.Equal(1.0, RetrievalMetrics.NdcgAtK(ideal, relevant, k)!.Value, Tolerance);

                if (k <= relevant.Count)
                {
                    Assert.Equal(1.0, RetrievalMetrics.PrecisionAtK(ideal, relevant, k), Tolerance);
                }
            }

            Assert.Equal(1.0, RetrievalMetrics.ReciprocalRank(ideal, relevant), Tolerance);
        }
    }
}

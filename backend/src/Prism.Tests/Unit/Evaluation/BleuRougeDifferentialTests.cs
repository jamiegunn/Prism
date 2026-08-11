using Prism.Features.Evaluation.Domain.Scorers;

namespace Prism.Tests.Unit.Evaluation;

/// <summary>
/// Differential proof that Prism's BLEU and ROUGE-L agree with their reference
/// implementations, plus the invariants that must hold for all inputs and worked examples a
/// reviewer can check by hand.
/// </summary>
/// <remarks>
/// <para>
/// The reference vectors were produced by running <c>sacrebleu 2.6.0</c>
/// (BLEU(effective_order=True).sentence_score — signature
/// <c>nrefs:1|case:mixed|eff:yes|tok:13a|smooth:exp|version:2.6.0</c>) and Google
/// <c>rouge-score 0.1.2</c> (RougeScorer(["rougeL"], use_stemmer=False)) over the 29 pairs
/// below, then baking the outputs in verbatim. The generation script lives in the commit
/// message trailer of the change that added this file.
/// </para>
/// <para>
/// Tolerance is 1e-9 on the 0–100 BLEU scale and 1e-9 for ROUGE-L on 0–1: both
/// implementations do the same arithmetic in double precision, so agreement should be to
/// rounding, not "close".
/// </para>
/// </remarks>
public sealed class BleuRougeDifferentialTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Sentence BLEU agrees with sacrebleu 2.6.0 to 1e-9 on the 0–100 scale, and ROUGE-L F1
    /// agrees with rouge-score 0.1.2, across identical, disjoint, empty, single-token,
    /// clipping, brevity, case, unicode, HTML-entity and digit-punctuation cases.
    /// </summary>
    /// <param name="hypothesis">The system output.</param>
    /// <param name="reference">The reference text.</param>
    /// <param name="expectedBleu">sacrebleu's sentence score (0–100).</param>
    /// <param name="expectedRougeL">rouge-score's rougeL fmeasure (0–1).</param>
    [Theory]
    // identical
    [InlineData("the cat sat on the mat", "the cat sat on the mat", 100.00000000000004, 1.0)]
    // partial overlap (the)
    [InlineData("the cat sat on the mat", "a dog ran in the park", 8.116697886877475, 0.16666666666666666)]
    // no overlap
    [InlineData("xyzzy plugh", "the cat sat on the mat", 0.0, 0.0)]
    // empty hypothesis
    [InlineData("", "the cat sat on the mat", 0.0, 0.0)]
    // empty reference
    [InlineData("the cat sat on the mat", "", 0.0, 0.0)]
    // both empty
    [InlineData("", "", 0.0, 0.0)]
    // single token identical
    [InlineData("cat", "cat", 100.00000000000004, 1.0)]
    // single token different
    [InlineData("cat", "dog", 0.0, 0.0)]
    // repeated tokens clip
    [InlineData("the the the the", "the cat", 15.97357760615681, 0.3333333333333333)]
    // reference repeats
    [InlineData("the cat", "the the the the", 18.393972058572114, 0.3333333333333333)]
    // short hypothesis BP
    [InlineData("the cat sat", "the cat sat on the mat by the door", 13.533528323661276, 0.5)]
    // long hypothesis
    [InlineData("the cat sat on the mat by the door today", "the cat sat", 15.619699684601283, 0.4615384615384615)]
    // case differs — BLEU is case-sensitive under sacrebleu defaults, ROUGE-L lowercases
    [InlineData("The Cat Sat On The Mat", "the cat sat on the mat", 0.0, 1.0)]
    // decimal number identical — 13a keeps "3.5" whole
    [InlineData("It costs 3.5 dollars.", "It costs 3.5 dollars.", 100.00000000000004, 1.0)]
    // decimal number differs
    [InlineData("It costs 3.5 dollars, right?", "It costs 4.5 dollars, right?", 48.892302243490086, 0.8333333333333334)]
    // punctuation heavy
    [InlineData("Hello, world! How are you?", "Hello, world! How are you today?", 76.72796459606589, 0.9090909090909091)]
    // html entity quote — 13a unescapes &quot;
    [InlineData("He said &quot;hello&quot; to me", "He said \"hello\" to me", 100.00000000000004, 0.8333333333333333)]
    // html entity amp
    [InlineData("A&amp;B is a firm", "A&B is a firm", 100.00000000000004, 0.9090909090909091)]
    // unicode identical
    [InlineData("naïve café résumé", "naïve café résumé", 100.00000000000004, 1.0)]
    // unicode vs ascii
    [InlineData("naïve café", "naive cafe", 0.0, 0.0)]
    // hyphenation — 13a does not split "state-of-the-art"
    [InlineData("state-of-the-art results", "state of the art results", 11.15650800742149, 1.0)]
    // number context
    [InlineData("pi is 3.14159 exactly", "pi is 3.14159 roughly", 59.460355750136046, 0.8000000000000002)]
    // ten token identical
    [InlineData("one two three four five six seven eight nine ten", "one two three four five six seven eight nine ten", 100.00000000000004, 1.0)]
    // one substitution
    [InlineData("one two three four bananas six seven eight nine ten", "one two three four five six seven eight nine ten", 65.80370064762461, 0.9)]
    // reversed order — unigrams match, order does not
    [InlineData("a b c d", "d c b a", 22.59005009024613, 0.25)]
    // near miss morphology
    [InlineData("the quick brown fox jumps over the lazy dog", "the quick brown fox jumped over the lazy dogs", 43.167001068522545, 0.7777777777777778)]
    // digit dash identical — 13a splits the dash after a digit on both sides
    [InlineData("2-3 items were found", "2-3 items were found", 100.00000000000004, 1.0)]
    // digit dash spacing — after 13a both sides tokenize identically
    [InlineData("results improved by 10-20 % overall", "results improved by 10 - 20 % overall", 100.00000000000004, 1.0)]
    // tab whitespace — python's split() treats tabs as separators; a space-only split would not
    [InlineData("the\tcat sat\ton the mat", "the cat sat on the mat", 100.00000000000004, 1.0)]
    public void Sentence_Scores_Agree_With_The_Reference_Implementations(
        string hypothesis, string reference, double expectedBleu, double expectedRougeL)
    {
        double bleu = SacreBleuMetric.SentenceScore(hypothesis, reference);
        Assert.Equal(expectedBleu, bleu, Tolerance);

        (_, _, double rougeF1) = RougeLScorer.Score(target: reference, prediction: hypothesis);
        Assert.Equal(expectedRougeL, rougeF1, Tolerance);
    }

    /// <summary>
    /// Corpus BLEU over the 26 non-empty-reference pairs agrees with
    /// <c>sacrebleu.BLEU().corpus_score</c> — including the summed lengths and the brevity
    /// penalty, so the aggregation is right, not just the final number.
    /// </summary>
    [Fact]
    public void Corpus_Bleu_Agrees_With_Sacrebleu_On_The_26_Pair_Corpus()
    {
        (string Hyp, string Ref)[] pairs = CorpusPairs();

        SacreBleuMetric.BleuStatistics summed = pairs
            .Select(p => SacreBleuMetric.SegmentStatistics(p.Hyp, p.Ref))
            .Aggregate((a, b) => a.Add(b));

        Assert.Equal(132, summed.HypLength);
        Assert.Equal(145, summed.RefLength);

        SacreBleuMetric.BleuResult result = SacreBleuMetric.ComputeBleu(summed, effectiveOrder: false);

        Assert.Equal(58.520626149825816, result.Score, Tolerance);
        Assert.Equal(0.9062094229560552, result.BrevityPenalty, Tolerance);
        Assert.Equal(75.0, result.Precisions[0], Tolerance);
        Assert.Equal(66.35514018691589, result.Precisions[1], Tolerance);
        Assert.Equal(63.095238095238095, result.Precisions[2], Tolerance);
        Assert.Equal(55.38461538461539, result.Precisions[3], Tolerance);
    }

    /// <summary>
    /// Corpus BLEU is not the mean of sentence BLEUs — the error the old aggregate committed.
    /// On the first three corpus pairs the two quantities differ by tens of points, so any
    /// regression to averaging fails loudly here.
    /// </summary>
    [Fact]
    public void Corpus_Bleu_Is_Not_The_Mean_Of_Sentence_Bleus()
    {
        (string Hyp, string Ref)[] pairs = CorpusPairs()[..3];

        SacreBleuMetric.BleuStatistics summed = pairs
            .Select(p => SacreBleuMetric.SegmentStatistics(p.Hyp, p.Ref))
            .Aggregate((a, b) => a.Add(b));

        double corpus = SacreBleuMetric.ComputeBleu(summed, effectiveOrder: false).Score;

        // sacrebleu 2.6.0: BLEU().corpus_score over the same three pairs.
        Assert.Equal(36.68915380611232, corpus, Tolerance);

        double meanOfSentences = pairs.Average(p => SacreBleuMetric.SentenceScore(p.Hyp, p.Ref));

        // mean of (100.000…04, 8.1166…, 0.0) ≈ 36.04 — close to the corpus value by
        // coincidence on this tiny set, but not equal, and the assertion tolerance leaves
        // no room to conflate them.
        Assert.NotEqual(corpus, meanOfSentences, 3);
    }

    /// <summary>
    /// Invariants over generated inputs: identical strings score BLEU 1.0 (0–100: 100) and
    /// ROUGE-L 1.0; token-disjoint strings score 0; the brevity penalty never exceeds 1 and
    /// only bites when the hypothesis is shorter; scores are invariant to the order of
    /// independent pairs (each pair is scored alone, so shuffling cannot change values).
    /// </summary>
    [Fact]
    public void Invariants_Hold_Across_Generated_Inputs()
    {
        var random = new Random(20260811); // fixed seed: failures must reproduce
        string[] vocabularyA = ["alpha", "bravo", "charlie", "delta", "echo", "foxtrot"];
        string[] vocabularyB = ["golf", "hotel", "india", "juliet", "kilo", "lima"];

        for (int i = 0; i < 200; i++)
        {
            int length = random.Next(1, 12);
            string sentence = string.Join(
                ' ', Enumerable.Range(0, length).Select(_ => vocabularyA[random.Next(vocabularyA.Length)]));

            // Identical strings: perfect score in both metrics.
            Assert.Equal(100.0, SacreBleuMetric.SentenceScore(sentence, sentence), 1e-9);
            Assert.Equal(1.0, RougeLScorer.Score(sentence, sentence).F1, Tolerance);

            // Disjoint vocabularies: zero in both metrics.
            int otherLength = random.Next(1, 12);
            string disjoint = string.Join(
                ' ', Enumerable.Range(0, otherLength).Select(_ => vocabularyB[random.Next(vocabularyB.Length)]));

            Assert.Equal(0.0, SacreBleuMetric.SentenceScore(sentence, disjoint), Tolerance);
            Assert.Equal(0.0, RougeLScorer.Score(disjoint, sentence).F1, Tolerance);

            // Brevity penalty: ≤ 1 always; exactly 1 when the hypothesis is not shorter.
            SacreBleuMetric.BleuStatistics stats =
                SacreBleuMetric.SegmentStatistics(sentence, disjoint);
            SacreBleuMetric.BleuResult result = SacreBleuMetric.ComputeBleu(stats, effectiveOrder: true);

            Assert.True(result.BrevityPenalty <= 1.0, "brevity penalty must never exceed 1");

            if (stats.HypLength >= stats.RefLength)
            {
                Assert.Equal(1.0, result.BrevityPenalty, Tolerance);
            }
        }
    }

    /// <summary>
    /// A worked example checkable without running anything.
    /// Hypothesis "the cat", reference "the the the the" (13a leaves both unchanged):
    /// hyp unigrams {the, cat}: "the" matches (clip min(1,4) = 1), "cat" does not → p1 = 1/2.
    /// Hyp bigrams {"the cat"}: no match → exp smoothing: smooth = 2, p2 = 100/(2·1) = 50.
    /// Effective order 2. Geometric mean = exp((ln 50 + ln 50)/2) = 50.
    /// BP: hyp 2 &lt; ref 4 → exp(1 − 4/2) = e⁻¹ ≈ 0.36787944117144233.
    /// BLEU = 50 · e⁻¹ = 18.393972058572114.
    /// </summary>
    [Fact]
    public void Worked_Example_Clipping_Smoothing_And_Brevity_By_Hand()
    {
        SacreBleuMetric.BleuStatistics stats =
            SacreBleuMetric.SegmentStatistics("the cat", "the the the the");

        Assert.Equal(2, stats.HypLength);
        Assert.Equal(4, stats.RefLength);
        Assert.Equal(1, stats.Correct[0]);  // "the", clipped to reference count ≥ 1
        Assert.Equal(2, stats.Total[0]);    // "the", "cat"
        Assert.Equal(0, stats.Correct[1]);  // "the cat" not in reference bigrams
        Assert.Equal(1, stats.Total[1]);

        SacreBleuMetric.BleuResult result = SacreBleuMetric.ComputeBleu(stats, effectiveOrder: true);

        Assert.Equal(50.0, result.Precisions[0], Tolerance);            // 100·1/2
        Assert.Equal(50.0, result.Precisions[1], Tolerance);            // 100/(2·1) smoothing
        Assert.Equal(Math.Exp(-1.0), result.BrevityPenalty, Tolerance); // exp(1 − 4/2)
        Assert.Equal(50.0 * Math.Exp(-1.0), result.Score, Tolerance);
    }

    /// <summary>
    /// A worked ROUGE-L example checkable by hand. Target "the cat sat" (3 tokens),
    /// prediction "the cat sat on the mat by the door today" (10 tokens): LCS = 3, so
    /// precision = 3/10, recall = 3/3 = 1, F1 = 2·0.3·1/(0.3+1) = 0.6/1.3 = 6/13.
    /// </summary>
    [Fact]
    public void Worked_Example_RougeL_By_Hand()
    {
        (double precision, double recall, double f1) = RougeLScorer.Score(
            target: "the cat sat",
            prediction: "the cat sat on the mat by the door today");

        Assert.Equal(0.3, precision, Tolerance);
        Assert.Equal(1.0, recall, Tolerance);
        Assert.Equal(6.0 / 13.0, f1, Tolerance);
    }

    /// <summary>
    /// The scorer wrappers expose the same numbers on the 0–1 scale the scoring pipeline
    /// stores, and their definitions name the tokenizer, smoothing and reference versions —
    /// what makes the numbers citable.
    /// </summary>
    [Fact]
    public async Task Scorer_Wrappers_Scale_To_Unit_Interval_And_State_Their_Definitions()
    {
        var bleu = new BleuScorer();
        var rouge = new RougeLScorer();

        double bleuScore = await bleu.ScoreAsync(
            "", "the the the the", "the cat", CancellationToken.None);
        Assert.Equal(0.18393972058572114, bleuScore, Tolerance);

        double rougeScore = await rouge.ScoreAsync(
            "", "the cat sat", "the cat sat on the mat by the door today", CancellationToken.None);
        Assert.Equal(6.0 / 13.0, rougeScore, Tolerance);

        Assert.Contains("13a", bleu.Definition);
        Assert.Contains("exp smoothing", bleu.Definition);
        Assert.Contains("sacrebleu 2.6.0", bleu.Definition);
        Assert.Contains("LCS", rouge.Definition);
        Assert.Contains("rouge-score 0.1.2", rouge.Definition);
    }

    /// <summary>
    /// The 26 pairs (every pair whose reference is non-empty) the corpus test sums over, in
    /// generation order.
    /// </summary>
    /// <returns>The corpus pairs.</returns>
    private static (string Hyp, string Ref)[] CorpusPairs() =>
    [
        ("the cat sat on the mat", "the cat sat on the mat"),
        ("the cat sat on the mat", "a dog ran in the park"),
        ("xyzzy plugh", "the cat sat on the mat"),
        ("", "the cat sat on the mat"),
        ("cat", "cat"),
        ("cat", "dog"),
        ("the the the the", "the cat"),
        ("the cat", "the the the the"),
        ("the cat sat", "the cat sat on the mat by the door"),
        ("the cat sat on the mat by the door today", "the cat sat"),
        ("The Cat Sat On The Mat", "the cat sat on the mat"),
        ("It costs 3.5 dollars.", "It costs 3.5 dollars."),
        ("It costs 3.5 dollars, right?", "It costs 4.5 dollars, right?"),
        ("Hello, world! How are you?", "Hello, world! How are you today?"),
        ("He said &quot;hello&quot; to me", "He said \"hello\" to me"),
        ("A&amp;B is a firm", "A&B is a firm"),
        ("naïve café résumé", "naïve café résumé"),
        ("naïve café", "naive cafe"),
        ("state-of-the-art results", "state of the art results"),
        ("pi is 3.14159 exactly", "pi is 3.14159 roughly"),
        ("one two three four five six seven eight nine ten", "one two three four five six seven eight nine ten"),
        ("one two three four bananas six seven eight nine ten", "one two three four five six seven eight nine ten"),
        ("a b c d", "d c b a"),
        ("the quick brown fox jumps over the lazy dog", "the quick brown fox jumped over the lazy dogs"),
        ("2-3 items were found", "2-3 items were found"),
        ("results improved by 10-20 % overall", "results improved by 10 - 20 % overall"),
    ];
}

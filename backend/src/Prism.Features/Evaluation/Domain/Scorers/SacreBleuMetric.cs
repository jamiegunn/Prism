using System.Text.RegularExpressions;

namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// A line-by-line port of sacrebleu's BLEU definition: the 13a tokenizer, clipped n-gram
/// statistics with closest-reference length, exp (NIST/mteval) smoothing, effective-order
/// handling for sentence scores, and the brevity penalty — so a number Prism reports can be
/// compared with published results.
/// </summary>
/// <remarks>
/// <para>
/// Ported from sacrebleu <see cref="ReferenceVersion"/> (Post, 2018,
/// "A Call for Clarity in Reporting BLEU Scores"). The differential test in
/// <c>BleuRougeDifferentialTests</c> pins agreement with the Python implementation to 1e-9 on
/// 29 sentence pairs and a 26-pair corpus, including empty, single-token, no-overlap,
/// clipping and brevity-penalty cases.
/// </para>
/// <para>
/// Scores are on sacrebleu's 0–100 scale; callers that need 0–1 divide by 100.
/// </para>
/// </remarks>
public static class SacreBleuMetric
{
    /// <summary>The sacrebleu version whose definition this port matches.</summary>
    public const string ReferenceVersion = "2.6.0";

    /// <summary>The maximum n-gram order, as in BLEU-4.</summary>
    public const int MaxNgramOrder = 4;

    // TokenizerRegexp from sacrebleu/tokenizers/tokenizer_re.py, in application order.
    private static readonly Regex PunctRegex = new(
        @"([{-~\[-` -&(-+:-@/])", RegexOptions.Compiled);

    private static readonly Regex PeriodCommaPrecededRegex = new(
        @"([^0-9])([\.,])", RegexOptions.Compiled);

    private static readonly Regex PeriodCommaFollowedRegex = new(
        @"([\.,])([^0-9])", RegexOptions.Compiled);

    private static readonly Regex DigitDashRegex = new(
        @"([0-9])(-)", RegexOptions.Compiled);

    /// <summary>
    /// Tokenizes a segment exactly as sacrebleu's <c>13a</c> tokenizer (mteval-v13a) does:
    /// unescape a fixed set of HTML entities, isolate punctuation, split periods and commas
    /// unless adjacent to digits, and split a dash preceded by a digit.
    /// </summary>
    /// <param name="line">The segment to tokenize. Not lowercased: BLEU is case-sensitive
    /// under sacrebleu's defaults.</param>
    /// <returns>The tokens.</returns>
    public static string[] Tokenize13a(string line)
    {
        // Tokenizer13a.__call__, language-independent part.
        line = line.Replace("<skipped>", "");
        line = line.Replace("-\n", "");
        line = line.Replace('\n', ' ');

        if (line.Contains('&'))
        {
            line = line.Replace("&quot;", "\"");
            line = line.Replace("&amp;", "&");
            line = line.Replace("&lt;", "<");
            line = line.Replace("&gt;", ">");
        }

        // TokenizerRegexp.__call__ on f' {line} '.
        line = $" {line} ";
        line = PunctRegex.Replace(line, " $1 ");
        line = PeriodCommaPrecededRegex.Replace(line, "$1 $2 ");
        line = PeriodCommaFollowedRegex.Replace(line, " $1 $2");
        line = DigitDashRegex.Replace(line, "$1 $2 ");

        // Python's str.split() with no argument splits on ALL whitespace — tabs included,
        // which the punctuation class does not cover. Splitting only on ' ' here would leave
        // a tab-joined token sacrebleu never produces.
        return line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// The sufficient statistics of one hypothesis/reference segment pair: lengths plus
    /// clipped n-gram match counts per order. Corpus BLEU sums these across segments;
    /// sentence BLEU computes directly from one.
    /// </summary>
    /// <param name="HypLength">Hypothesis length in tokens.</param>
    /// <param name="RefLength">Reference length in tokens (closest reference, with a single
    /// reference simply its length).</param>
    /// <param name="Correct">Clipped matching n-gram counts, index 0 = unigrams.</param>
    /// <param name="Total">Hypothesis n-gram counts, index 0 = unigrams.</param>
    public sealed record BleuStatistics(int HypLength, int RefLength, int[] Correct, int[] Total)
    {
        /// <summary>
        /// Adds another segment's statistics into this one, returning the sum — the corpus
        /// aggregation step. Corpus BLEU is computed from summed statistics, not by averaging
        /// per-sentence scores.
        /// </summary>
        /// <param name="other">The statistics to add.</param>
        /// <returns>The element-wise sum.</returns>
        public BleuStatistics Add(BleuStatistics other)
        {
            int[] correct = new int[MaxNgramOrder];
            int[] total = new int[MaxNgramOrder];

            for (int i = 0; i < MaxNgramOrder; i++)
            {
                correct[i] = Correct[i] + other.Correct[i];
                total[i] = Total[i] + other.Total[i];
            }

            return new BleuStatistics(
                HypLength + other.HypLength, RefLength + other.RefLength, correct, total);
        }
    }

    /// <summary>
    /// A computed BLEU score with its components.
    /// </summary>
    /// <param name="Score">The BLEU score on sacrebleu's 0–100 scale.</param>
    /// <param name="BrevityPenalty">The brevity penalty in (0, 1].</param>
    /// <param name="Precisions">Per-order precisions on the 0–100 scale.</param>
    public sealed record BleuResult(double Score, double BrevityPenalty, double[] Precisions);

    /// <summary>
    /// Computes the segment statistics for one hypothesis against one reference, tokenizing
    /// both with 13a.
    /// </summary>
    /// <param name="hypothesis">The system output.</param>
    /// <param name="reference">The reference text.</param>
    /// <returns>The sufficient statistics.</returns>
    public static BleuStatistics SegmentStatistics(string hypothesis, string reference)
    {
        string[] hypTokens = Tokenize13a(hypothesis.TrimEnd());
        string[] refTokens = Tokenize13a(reference.TrimEnd());

        Dictionary<string, int> refNgrams = CountNgrams(refTokens);
        Dictionary<string, int> hypNgrams = CountNgrams(hypTokens);

        int[] correct = new int[MaxNgramOrder];
        int[] total = new int[MaxNgramOrder];

        foreach ((string ngram, int hypCount) in hypNgrams)
        {
            int order = CountSpaces(ngram);
            total[order] += hypCount;

            if (refNgrams.TryGetValue(ngram, out int refCount))
            {
                correct[order] += Math.Min(hypCount, refCount);
            }
        }

        return new BleuStatistics(hypTokens.Length, refTokens.Length, correct, total);
    }

    /// <summary>
    /// Computes BLEU from sufficient statistics — sacrebleu's <c>compute_bleu</c> with
    /// <c>smooth_method='exp'</c>. Sentence-level scoring passes
    /// <paramref name="effectiveOrder"/> = true; corpus-level passes false.
    /// </summary>
    /// <param name="stats">The (possibly summed) statistics.</param>
    /// <param name="effectiveOrder">Whether to stop at the highest order that has any
    /// hypothesis n-grams, which sentence BLEU requires to avoid zeroing every short
    /// sentence.</param>
    /// <returns>The score with its components.</returns>
    public static BleuResult ComputeBleu(BleuStatistics stats, bool effectiveOrder)
    {
        double brevityPenalty = 1.0;

        if (stats.HypLength < stats.RefLength)
        {
            brevityPenalty = stats.HypLength > 0
                ? Math.Exp(1 - (double)stats.RefLength / stats.HypLength)
                : 0.0;
        }

        double[] precisions = new double[MaxNgramOrder];

        // Early exit when nothing matched at any order (#141 in sacrebleu).
        if (stats.Correct.All(c => c == 0))
        {
            return new BleuResult(0.0, brevityPenalty, precisions);
        }

        double smoothMteval = 1.0;
        int effOrder = MaxNgramOrder;

        for (int n = 1; n <= MaxNgramOrder; n++)
        {
            if (stats.Total[n - 1] == 0)
            {
                break;
            }

            if (effectiveOrder)
            {
                effOrder = n;
            }

            if (stats.Correct[n - 1] == 0)
            {
                // exp smoothing (Chen & Cherry method 3, the mteval-v13a default).
                smoothMteval *= 2;
                precisions[n - 1] = 100.0 / (smoothMteval * stats.Total[n - 1]);
            }
            else
            {
                precisions[n - 1] = 100.0 * stats.Correct[n - 1] / stats.Total[n - 1];
            }
        }

        double logSum = 0.0;
        for (int n = 0; n < effOrder; n++)
        {
            logSum += FlooredLog(precisions[n]);
        }

        double score = brevityPenalty * Math.Exp(logSum / effOrder);

        return new BleuResult(score, brevityPenalty, precisions);
    }

    /// <summary>
    /// Computes a sentence-level BLEU score (0–100) for one hypothesis against one
    /// reference, with sacrebleu's sentence defaults (effective order on).
    /// </summary>
    /// <param name="hypothesis">The system output.</param>
    /// <param name="reference">The reference text.</param>
    /// <returns>The sentence BLEU score on the 0–100 scale.</returns>
    public static double SentenceScore(string hypothesis, string reference) =>
        ComputeBleu(SegmentStatistics(hypothesis, reference), effectiveOrder: true).Score;

    /// <summary>
    /// sacrebleu's <c>my_log</c>: log floored at a very large negative number so a zero
    /// precision drives the score to zero instead of throwing.
    /// </summary>
    /// <param name="value">The value to take the log of.</param>
    /// <returns>The natural log, or the floor for zero.</returns>
    private static double FlooredLog(double value) => value == 0.0 ? -9999999999 : Math.Log(value);

    private static Dictionary<string, int> CountNgrams(string[] tokens)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int n = 1; n <= MaxNgramOrder; n++)
        {
            for (int i = 0; i + n <= tokens.Length; i++)
            {
                // '' cannot appear in whitespace-split tokens, so joining with it is a
                // collision-free n-gram key; the space count encodes the order.
                string key = string.Join(' ', tokens[i..(i + n)]);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        return counts;
    }

    private static int CountSpaces(string ngram)
    {
        int count = 0;
        foreach (char c in ngram)
        {
            if (c == ' ')
            {
                count++;
            }
        }

        return count;
    }
}

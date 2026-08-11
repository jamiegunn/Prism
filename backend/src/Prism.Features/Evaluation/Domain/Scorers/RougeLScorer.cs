using System.Text.RegularExpressions;

namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// ROUGE-L F1 with Google's <c>rouge-score</c> definition: lowercase, strip non-alphanumerics,
/// sentence-level longest common subsequence, F1 (β = 1), no stemming.
/// </summary>
/// <remarks>
/// Ported from <c>rouge-score</c> <see cref="ReferenceVersion"/> (`rouge_scorer.py` /
/// `tokenize.py`), the reference implementation papers cite for ROUGE outside the original
/// Perl. The differential test in <c>BleuRougeDifferentialTests</c> pins agreement to 1e-9 on
/// 29 pairs including empty, single-token, no-overlap and unicode cases.
/// </remarks>
public sealed class RougeLScorer : IScoringMethod
{
    /// <summary>The rouge-score version whose definition this port matches.</summary>
    public const string ReferenceVersion = "0.1.2";

    private static readonly Regex NonAlphanumRegex = new("[^a-z0-9]+", RegexOptions.Compiled);

    /// <inheritdoc />
    public string Name => "rouge_l";

    /// <inheritdoc />
    public string Definition =>
        "ROUGE-L F1 (β = 1): sentence-level LCS, lowercased, non-alphanumerics stripped, " +
        $"no stemming. Definition ported from Google rouge-score {ReferenceVersion}, " +
        "differential-tested to 1e-9. Scale 0–1.";

    /// <inheritdoc />
    public Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct)
    {
        return Task.FromResult(Score(target: expected, prediction: actual).F1);
    }

    /// <summary>
    /// Computes ROUGE-L precision, recall and F1 for a prediction against a target.
    /// </summary>
    /// <param name="target">The reference (ground-truth) text.</param>
    /// <param name="prediction">The system output.</param>
    /// <returns>The three components, each in [0, 1].</returns>
    public static (double Precision, double Recall, double F1) Score(string target, string prediction)
    {
        string[] targetTokens = Tokenize(target);
        string[] predictionTokens = Tokenize(prediction);

        if (targetTokens.Length == 0 || predictionTokens.Length == 0)
        {
            return (0.0, 0.0, 0.0);
        }

        int lcs = LcsLength(targetTokens, predictionTokens);

        double precision = (double)lcs / predictionTokens.Length;
        double recall = (double)lcs / targetTokens.Length;
        double f1 = precision + recall > 0
            ? 2 * precision * recall / (precision + recall)
            : 0.0;

        return (precision, recall, f1);
    }

    /// <summary>
    /// Tokenizes exactly as <c>rouge_score.tokenize.tokenize</c> with no stemmer: lowercase,
    /// replace runs of non-alphanumerics with spaces, split, and keep only tokens that are
    /// purely <c>[a-z0-9]+</c>.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <returns>The tokens.</returns>
    internal static string[] Tokenize(string text)
    {
        string lowered = text.ToLowerInvariant();
        string spaced = NonAlphanumRegex.Replace(lowered, " ");

        return spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Bottom-up LCS table, as in <c>rouge_scorer._lcs_table</c>, reduced to two rows.
    /// </summary>
    /// <param name="reference">The reference tokens.</param>
    /// <param name="candidate">The candidate tokens.</param>
    /// <returns>The length of the longest common subsequence.</returns>
    private static int LcsLength(string[] reference, string[] candidate)
    {
        int n = candidate.Length;
        int[] previous = new int[n + 1];
        int[] current = new int[n + 1];

        foreach (string refToken in reference)
        {
            for (int j = 1; j <= n; j++)
            {
                current[j] = string.Equals(refToken, candidate[j - 1], StringComparison.Ordinal)
                    ? previous[j - 1] + 1
                    : Math.Max(previous[j], current[j - 1]);
            }

            (previous, current) = (current, previous);
            Array.Clear(current);
        }

        return previous[n];
    }
}

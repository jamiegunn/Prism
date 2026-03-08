namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// Computes a simplified BLEU (Bilingual Evaluation Understudy) score.
/// Uses up to 4-gram precision with a brevity penalty. Values range from 0.0 to 1.0.
/// </summary>
public sealed class BleuScorer : IScoringMethod
{
    private const int MaxN = 4;

    /// <inheritdoc />
    public string Name => "bleu";

    /// <inheritdoc />
    public Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct)
    {
        string[] referenceTokens = Tokenize(expected);
        string[] candidateTokens = Tokenize(actual);

        if (candidateTokens.Length == 0 || referenceTokens.Length == 0)
        {
            return Task.FromResult(0.0);
        }

        // Compute n-gram precisions
        double logSum = 0;
        int count = 0;

        for (int n = 1; n <= MaxN; n++)
        {
            (int matches, int total) = ComputeNgramPrecision(referenceTokens, candidateTokens, n);
            if (total == 0)
            {
                break;
            }

            // Smoothed precision to avoid log(0)
            double precision = (matches + 1.0) / (total + 1.0);
            logSum += Math.Log(precision);
            count++;
        }

        if (count == 0)
        {
            return Task.FromResult(0.0);
        }

        double geometricMean = Math.Exp(logSum / count);

        // Brevity penalty
        double bp = candidateTokens.Length >= referenceTokens.Length
            ? 1.0
            : Math.Exp(1.0 - (double)referenceTokens.Length / candidateTokens.Length);

        return Task.FromResult(bp * geometricMean);
    }

    private static string[] Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static (int matches, int total) ComputeNgramPrecision(string[] reference, string[] candidate, int n)
    {
        if (candidate.Length < n || reference.Length < n)
        {
            return (0, 0);
        }

        Dictionary<string, int> refCounts = new();
        for (int i = 0; i <= reference.Length - n; i++)
        {
            string ngram = string.Join(" ", reference.AsSpan(i, n).ToArray());
            refCounts[ngram] = refCounts.GetValueOrDefault(ngram) + 1;
        }

        int matches = 0;
        int total = 0;
        for (int i = 0; i <= candidate.Length - n; i++)
        {
            string ngram = string.Join(" ", candidate.AsSpan(i, n).ToArray());
            total++;
            if (refCounts.TryGetValue(ngram, out int remaining) && remaining > 0)
            {
                matches++;
                refCounts[ngram] = remaining - 1;
            }
        }

        return (matches, total);
    }
}

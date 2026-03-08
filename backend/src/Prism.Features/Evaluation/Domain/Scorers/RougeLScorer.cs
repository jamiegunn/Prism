namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// Computes ROUGE-L (Longest Common Subsequence) F1 score between expected and actual output.
/// Values range from 0.0 to 1.0.
/// </summary>
public sealed class RougeLScorer : IScoringMethod
{
    /// <inheritdoc />
    public string Name => "rouge_l";

    /// <inheritdoc />
    public Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct)
    {
        string[] expectedTokens = Tokenize(expected);
        string[] actualTokens = Tokenize(actual);

        if (expectedTokens.Length == 0 || actualTokens.Length == 0)
        {
            return Task.FromResult(0.0);
        }

        int lcsLength = ComputeLcsLength(expectedTokens, actualTokens);

        double precision = (double)lcsLength / actualTokens.Length;
        double recall = (double)lcsLength / expectedTokens.Length;

        double f1 = (precision + recall) > 0
            ? 2.0 * precision * recall / (precision + recall)
            : 0.0;

        return Task.FromResult(f1);
    }

    private static string[] Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static int ComputeLcsLength(string[] a, string[] b)
    {
        int m = a.Length;
        int n = b.Length;
        int[] previous = new int[n + 1];
        int[] current = new int[n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (string.Equals(a[i - 1], b[j - 1], StringComparison.Ordinal))
                {
                    current[j] = previous[j - 1] + 1;
                }
                else
                {
                    current[j] = Math.Max(previous[j], current[j - 1]);
                }
            }

            (previous, current) = (current, previous);
            Array.Clear(current, 0, current.Length);
        }

        return previous[n];
    }
}

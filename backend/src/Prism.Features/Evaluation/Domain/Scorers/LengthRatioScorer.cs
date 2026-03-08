namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// Scores based on how close the actual output length is to the expected output length.
/// Returns a value between 0.0 and 1.0, where 1.0 means identical lengths.
/// Useful for detecting models that are too verbose or too brief.
/// </summary>
public sealed class LengthRatioScorer : IScoringMethod
{
    /// <inheritdoc />
    public string Name => "length_ratio";

    /// <inheritdoc />
    public Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct)
    {
        int expectedLen = expected.Trim().Length;
        int actualLen = actual.Trim().Length;

        if (expectedLen == 0 && actualLen == 0)
        {
            return Task.FromResult(1.0);
        }

        if (expectedLen == 0 || actualLen == 0)
        {
            return Task.FromResult(0.0);
        }

        double ratio = (double)Math.Min(expectedLen, actualLen) / Math.Max(expectedLen, actualLen);
        return Task.FromResult(ratio);
    }
}

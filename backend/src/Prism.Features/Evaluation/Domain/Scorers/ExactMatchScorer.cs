namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// Scores by exact string match (case-insensitive, trimmed).
/// Returns 1.0 for a match, 0.0 otherwise.
/// </summary>
public sealed class ExactMatchScorer : IScoringMethod
{
    /// <inheritdoc />
    public string Name => "exact_match";

    /// <inheritdoc />
    public Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct)
    {
        double score = string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase)
            ? 1.0
            : 0.0;
        return Task.FromResult(score);
    }
}

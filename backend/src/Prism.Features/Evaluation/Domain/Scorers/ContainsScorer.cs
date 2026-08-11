namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// Scores 1.0 if the actual output contains the expected output (case-insensitive), 0.0 otherwise.
/// Useful for checking if key information is present in the response.
/// </summary>
public sealed class ContainsScorer : IScoringMethod
{
    /// <inheritdoc />
    public string Name => "contains";

    /// <inheritdoc />
    public string Definition =>
        "Substring containment: 1.0 when the output contains the trimmed expected text, case-insensitive; else 0.0.";

    /// <inheritdoc />
    public Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct)
    {
        double score = actual.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase)
            ? 1.0
            : 0.0;
        return Task.FromResult(score);
    }
}

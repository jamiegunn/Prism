namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Contains computed token-level analysis metrics for an inference run.
/// Produced by <see cref="ITokenAnalysisService"/> from logprobs data.
/// </summary>
public sealed record TokenAnalysis
{
    /// <summary>
    /// Gets the perplexity of the response. Lower values indicate higher model confidence.
    /// Computed as exp(average negative logprob).
    /// </summary>
    public double Perplexity { get; init; }

    /// <summary>
    /// Gets the average logprob across all generated tokens.
    /// </summary>
    public double AverageLogprob { get; init; }

    /// <summary>
    /// Gets the Shannon entropy value for each token position.
    /// High entropy indicates the model was torn between alternatives.
    /// </summary>
    public IReadOnlyList<double> EntropyPerToken { get; init; } = [];

    /// <summary>
    /// Gets the mean entropy across all token positions.
    /// </summary>
    public double MeanEntropy { get; init; }

    /// <summary>
    /// Gets tokens where the model's confidence fell below the surprise threshold.
    /// These represent "interesting" tokens where the model made non-obvious choices.
    /// </summary>
    public IReadOnlyList<SurpriseToken> SurpriseTokens { get; init; } = [];

    /// <summary>
    /// Gets the total number of tokens analyzed.
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Gets whether this analysis contains meaningful data.
    /// False when the provider did not return logprobs.
    /// </summary>
    public bool HasData { get; init; }

    /// <summary>
    /// Returns an empty analysis for cases where logprobs are unavailable.
    /// </summary>
    public static TokenAnalysis Empty => new() { HasData = false };
}

/// <summary>
/// Represents a token that was generated with unexpectedly low probability.
/// </summary>
public sealed record SurpriseToken
{
    /// <summary>
    /// Gets the token text.
    /// </summary>
    public string Token { get; init; } = "";

    /// <summary>
    /// Gets the logprob value for this token.
    /// </summary>
    public double Logprob { get; init; }

    /// <summary>
    /// Gets the probability (exp of logprob) for this token.
    /// </summary>
    public double Probability { get; init; }

    /// <summary>
    /// Gets the position index within the generated sequence.
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// Gets the Shannon entropy at this token position.
    /// </summary>
    public double Entropy { get; init; }
}

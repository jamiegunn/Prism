namespace Prism.Common.Inference.Models;

/// <summary>
/// Contains log probability information for all tokens in a completion.
/// </summary>
public sealed record LogprobsData
{
    /// <summary>
    /// Gets or initializes the per-token log probability entries.
    /// </summary>
    public List<TokenLogprob> Tokens { get; init; } = [];
}

/// <summary>
/// Represents log probability information for a single generated token.
/// </summary>
public sealed record TokenLogprob
{
    /// <summary>
    /// Gets or initializes the token text.
    /// </summary>
    public string Token { get; init; } = "";

    /// <summary>
    /// Gets or initializes the log probability of this token.
    /// </summary>
    public double Logprob { get; init; }

    /// <summary>
    /// Gets the linear probability of this token, computed as exp(logprob).
    /// </summary>
    public double Probability => Math.Exp(Logprob);

    /// <summary>
    /// Gets or initializes the most likely alternative tokens and their log probabilities.
    /// </summary>
    public List<TopLogprob> TopLogprobs { get; init; } = [];

    /// <summary>
    /// Gets or initializes the byte offset of this token in the output, if available.
    /// </summary>
    public int? ByteOffset { get; init; }
}

/// <summary>
/// Represents a single alternative token and its log probability in the top-K list.
/// </summary>
public sealed record TopLogprob
{
    /// <summary>
    /// Gets or initializes the alternative token text.
    /// </summary>
    public string Token { get; init; } = "";

    /// <summary>
    /// Gets or initializes the log probability of this alternative token.
    /// </summary>
    public double Logprob { get; init; }

    /// <summary>
    /// Gets the linear probability of this alternative token, computed as exp(logprob).
    /// </summary>
    public double Probability => Math.Exp(Logprob);
}

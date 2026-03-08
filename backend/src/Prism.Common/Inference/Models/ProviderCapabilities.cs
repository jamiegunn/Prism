namespace Prism.Common.Inference.Models;

/// <summary>
/// Describes the capabilities and feature support of an inference provider.
/// Used to dynamically enable/disable UI features based on what the provider supports.
/// </summary>
public sealed record ProviderCapabilities
{
    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports chat completions.
    /// </summary>
    public bool SupportsChat { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports streaming responses via SSE.
    /// </summary>
    public bool SupportsStreaming { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports returning log probabilities.
    /// </summary>
    public bool SupportsLogprobs { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of top log probabilities the provider can return per token.
    /// </summary>
    public int MaxTopLogprobs { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports tokenization endpoints.
    /// </summary>
    public bool SupportsTokenize { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports guided decoding (JSON schema, regex).
    /// </summary>
    public bool SupportsGuidedDecoding { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the provider exposes performance metrics.
    /// </summary>
    public bool SupportsMetrics { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports hot-reloading models.
    /// </summary>
    public bool SupportsHotReload { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports system messages.
    /// </summary>
    public bool SupportsSystemMessages { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports the frequency penalty parameter.
    /// </summary>
    public bool SupportsFrequencyPenalty { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports the presence penalty parameter.
    /// </summary>
    public bool SupportsPresencePenalty { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether the provider supports stop sequences.
    /// </summary>
    public bool SupportsStopSequences { get; init; } = true;
}

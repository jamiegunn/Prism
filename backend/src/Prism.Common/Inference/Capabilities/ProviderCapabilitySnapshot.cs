namespace Prism.Common.Inference.Capabilities;

/// <summary>
/// A point-in-time snapshot of a provider's probed capabilities.
/// Produced by <see cref="IProviderCapabilityRegistry"/> and used by features and UI
/// to determine which controls and features to enable.
/// </summary>
public sealed record ProviderCapabilitySnapshot
{
    /// <summary>
    /// Gets the provider instance ID this snapshot belongs to.
    /// </summary>
    public Guid InstanceId { get; init; }

    /// <summary>
    /// Gets the provider display name.
    /// </summary>
    public string ProviderName { get; init; } = "";

    /// <summary>
    /// Gets the computed capability tier.
    /// </summary>
    public CapabilityTier Tier { get; init; }

    /// <summary>
    /// Gets whether the provider supports returning log probabilities.
    /// </summary>
    public bool SupportsLogprobs { get; init; }

    /// <summary>
    /// Gets the maximum number of top logprobs the provider can return per token.
    /// </summary>
    public int MaxLogprobs { get; init; }

    /// <summary>
    /// Gets whether the provider supports tokenization endpoints.
    /// </summary>
    public bool SupportsTokenize { get; init; }

    /// <summary>
    /// Gets whether the provider supports guided decoding (JSON schema, regex constraints).
    /// </summary>
    public bool SupportsGuidedDecoding { get; init; }

    /// <summary>
    /// Gets whether the provider supports streaming responses.
    /// </summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>
    /// Gets whether the provider supports function/tool calling.
    /// </summary>
    public bool SupportsFunctionCalling { get; init; }

    /// <summary>
    /// Gets whether the provider exposes performance metrics.
    /// </summary>
    public bool SupportsMetrics { get; init; }

    /// <summary>
    /// Gets whether the provider supports hot-swapping models.
    /// </summary>
    public bool SupportsModelSwap { get; init; }

    /// <summary>
    /// Gets whether the provider supports multimodal inputs.
    /// </summary>
    public bool SupportsMultimodal { get; init; }

    /// <summary>
    /// Gets when this snapshot was produced.
    /// </summary>
    public DateTime ProbedAt { get; init; }

    /// <summary>
    /// Gets whether the probe succeeded. If false, capabilities reflect safe defaults (Chat tier).
    /// </summary>
    public bool ProbeSucceeded { get; init; }

    /// <summary>
    /// Gets the error message if the probe failed.
    /// </summary>
    public string? ProbeError { get; init; }
}

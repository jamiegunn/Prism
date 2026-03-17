namespace Prism.Common.Inference.Capabilities;

/// <summary>
/// Classifies a provider's capability level for quick UI decision-making.
/// Individual capability flags are the source of truth; tiers are convenience labels.
/// </summary>
public enum CapabilityTier
{
    /// <summary>
    /// Capabilities have not been probed or probing failed.
    /// UI should treat all research features as potentially unavailable.
    /// </summary>
    Unknown,

    /// <summary>
    /// Chat and streaming only. No logprobs, tokenization, or guided decoding.
    /// Suitable for basic chat but not research features.
    /// </summary>
    Chat,

    /// <summary>
    /// Streaming, logprobs (possibly limited top-K), and partial metrics.
    /// Suitable for basic token inspection but not full research features.
    /// </summary>
    Inspect,

    /// <summary>
    /// Full research capabilities: logprobs, tokenization, guided decoding, streaming, and metrics.
    /// All research features are available.
    /// </summary>
    Research
}

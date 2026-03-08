using Prism.Common.Database;
using Prism.Common.Inference;

namespace Prism.Features.Models.Domain;

/// <summary>
/// Represents a registered inference provider instance (e.g., a vLLM server, Ollama instance).
/// Tracks connection details, capabilities, health status, and the currently loaded model.
/// </summary>
public sealed class InferenceInstance : BaseEntity
{
    /// <summary>
    /// Gets or sets the display name of this instance.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the base endpoint URL of the inference provider.
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// Gets or sets the type of inference provider (vLLM, Ollama, LM Studio, etc.).
    /// </summary>
    public InferenceProviderType ProviderType { get; set; }

    /// <summary>
    /// Gets or sets the current operational status of this instance.
    /// </summary>
    public InstanceStatus Status { get; set; } = InstanceStatus.Unknown;

    /// <summary>
    /// Gets or sets the identifier of the model currently loaded on this instance.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Gets or sets the GPU configuration description (e.g., "1x RTX 4090 24GB").
    /// </summary>
    public string? GpuConfig { get; set; }

    /// <summary>
    /// Gets or sets the maximum context length in tokens supported by the loaded model.
    /// </summary>
    public int? MaxContextLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports returning log probabilities.
    /// </summary>
    public bool SupportsLogprobs { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of top log probabilities the provider can return per token.
    /// </summary>
    public int MaxTopLogprobs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports streaming responses.
    /// </summary>
    public bool SupportsStreaming { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider exposes performance metrics.
    /// </summary>
    public bool SupportsMetrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports tokenization endpoints.
    /// </summary>
    public bool SupportsTokenize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports guided decoding (JSON schema, regex).
    /// </summary>
    public bool SupportsGuidedDecoding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports multimodal inputs.
    /// </summary>
    public bool SupportsMultimodal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider supports hot-swapping models.
    /// </summary>
    public bool SupportsModelSwap { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the default instance for inference requests.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the last health check.
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>
    /// Gets or sets the error message from the last failed health check, if any.
    /// </summary>
    public string? LastHealthError { get; set; }

    /// <summary>
    /// Gets or sets the user-defined tags for categorizing this instance.
    /// </summary>
    public List<string> Tags { get; set; } = [];
}

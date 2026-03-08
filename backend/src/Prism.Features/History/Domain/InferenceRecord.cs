using Prism.Common.Database;
using Prism.Common.Inference;

namespace Prism.Features.History.Domain;

/// <summary>
/// Represents a persisted inference record capturing the full request/response lifecycle.
/// Stores serialized JSON for request, response, and environment data to preserve exact payloads.
/// </summary>
public sealed class InferenceRecord : BaseEntity
{
    /// <summary>
    /// Gets or sets the module that originated this inference request (e.g., "playground", "experiments").
    /// </summary>
    public string SourceModule { get; set; } = "";

    /// <summary>
    /// Gets or sets the display name of the provider that handled the request.
    /// </summary>
    public string ProviderName { get; set; } = "";

    /// <summary>
    /// Gets or sets the type of inference provider used.
    /// </summary>
    public InferenceProviderType ProviderType { get; set; }

    /// <summary>
    /// Gets or sets the endpoint URL of the provider that handled the request.
    /// </summary>
    public string ProviderEndpoint { get; set; } = "";

    /// <summary>
    /// Gets or sets the model identifier used for inference.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// Gets or sets the serialized <c>ChatRequest</c> as JSON.
    /// </summary>
    public string RequestJson { get; set; } = "";

    /// <summary>
    /// Gets or sets the serialized <c>ChatResponse</c> as JSON, or null if the request failed.
    /// </summary>
    public string? ResponseJson { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens in the prompt.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens in the generated completion.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Gets or sets the total number of tokens used (prompt + completion).
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the total request latency in milliseconds.
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the time to first token in milliseconds, if available.
    /// </summary>
    public int? TtftMs { get; set; }

    /// <summary>
    /// Gets or sets the perplexity score for the response, if computed.
    /// </summary>
    public double? Perplexity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the inference request completed successfully.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the error message if the request failed, or null on success.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the user-defined tags for categorizing this record.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when the inference request started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the inference request completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the serialized <c>EnvironmentSnapshot</c> as JSON, or null if not captured.
    /// </summary>
    public string? EnvironmentJson { get; set; }
}

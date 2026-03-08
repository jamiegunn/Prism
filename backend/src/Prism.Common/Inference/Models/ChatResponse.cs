namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents a provider-agnostic chat completion response.
/// </summary>
public sealed record ChatResponse
{
    /// <summary>
    /// Gets or initializes the generated text content.
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// Gets or initializes the reason generation stopped (e.g., "stop", "length", "content_filter").
    /// </summary>
    public string? FinishReason { get; init; }

    /// <summary>
    /// Gets or initializes the token usage information.
    /// </summary>
    public UsageInfo? Usage { get; init; }

    /// <summary>
    /// Gets or initializes the log probability data for the generated tokens.
    /// Only populated when logprobs are requested.
    /// </summary>
    public LogprobsData? LogprobsData { get; init; }

    /// <summary>
    /// Gets or initializes the model identifier that generated this response.
    /// </summary>
    public string ModelId { get; init; } = "";

    /// <summary>
    /// Gets or initializes the timing information for this request.
    /// </summary>
    public TimingInfo? Timing { get; init; }
}

/// <summary>
/// Token usage statistics for a chat completion.
/// </summary>
/// <param name="PromptTokens">The number of tokens in the prompt.</param>
/// <param name="CompletionTokens">The number of tokens in the generated completion.</param>
/// <param name="TotalTokens">The total number of tokens used (prompt + completion).</param>
public sealed record UsageInfo(int PromptTokens, int CompletionTokens, int TotalTokens);

/// <summary>
/// Performance timing information for an inference request.
/// </summary>
/// <param name="LatencyMs">The total latency in milliseconds from request to response.</param>
/// <param name="TtftMs">Time to first token in milliseconds, if streaming.</param>
/// <param name="TokensPerSecond">The generation throughput in tokens per second.</param>
public sealed record TimingInfo(long LatencyMs, long? TtftMs, double? TokensPerSecond);

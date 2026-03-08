namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents a single chunk from a streaming chat completion response (SSE event).
/// Each chunk typically contains a single generated token.
/// </summary>
public sealed record StreamChunk
{
    /// <summary>
    /// Gets or initializes the text content of this chunk (typically a single token).
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// Gets or initializes the index of this chunk in the stream sequence.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets or initializes the log probability entry for this token, if logprobs were requested.
    /// </summary>
    public TokenLogprob? LogprobsEntry { get; init; }

    /// <summary>
    /// Gets or initializes the finish reason, set on the final chunk.
    /// </summary>
    public string? FinishReason { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether this is the first chunk in the stream.
    /// Useful for measuring time-to-first-token (TTFT).
    /// </summary>
    public bool IsFirst { get; init; }

    /// <summary>
    /// Gets or initializes the usage information, typically included in the final chunk.
    /// </summary>
    public UsageInfo? Usage { get; init; }
}

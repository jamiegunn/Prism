namespace Prism.Features.Playground.Domain;

/// <summary>
/// Represents a single message within a playground conversation.
/// Stores content, role, logprobs data, and performance metrics.
/// </summary>
public sealed class Message : BaseEntity
{
    /// <summary>
    /// Gets or sets the ID of the conversation this message belongs to.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the role of the message sender.
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// Gets or sets the text content of the message.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Gets or sets the number of tokens in this message.
    /// </summary>
    public int? TokenCount { get; set; }

    /// <summary>
    /// Gets or sets the serialized log probabilities data as JSON.
    /// </summary>
    public string? LogprobsJson { get; set; }

    /// <summary>
    /// Gets or sets the calculated perplexity of the generated response.
    /// </summary>
    public double? Perplexity { get; set; }

    /// <summary>
    /// Gets or sets the total latency in milliseconds for the inference request.
    /// </summary>
    public int? LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the time to first token in milliseconds.
    /// </summary>
    public int? TtftMs { get; set; }

    /// <summary>
    /// Gets or sets the generation throughput in tokens per second.
    /// </summary>
    public double? TokensPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the reason generation stopped (e.g., "stop", "length").
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// Gets or sets the ordering index of this message within the conversation.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets the parent conversation navigation property.
    /// </summary>
    public Conversation? Conversation { get; set; }
}

namespace Prism.Features.Playground.Domain;

/// <summary>
/// Aggregate root representing a playground chat conversation.
/// Contains messages, inference parameters, and usage statistics.
/// </summary>
public sealed class Conversation : BaseEntity
{
    /// <summary>
    /// Gets or sets the title of the conversation, auto-generated from the first user message.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional system prompt for the conversation.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Gets or sets the model identifier used for this conversation.
    /// </summary>
    public string ModelId { get; set; } = "";

    /// <summary>
    /// Gets or sets the inference provider instance ID used for this conversation.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the inference parameters for this conversation.
    /// </summary>
    public ConversationParameters Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the messages in this conversation.
    /// </summary>
    public List<Message> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this conversation is pinned.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Gets or sets the running total of tokens used in this conversation.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the last message in this conversation.
    /// </summary>
    public DateTime? LastMessageAt { get; set; }
}

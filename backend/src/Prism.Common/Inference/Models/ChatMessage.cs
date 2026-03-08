namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents a single message in a chat conversation.
/// </summary>
/// <param name="Role">The role of the message sender (system, user, or assistant).</param>
/// <param name="Content">The text content of the message.</param>
/// <param name="Name">An optional name for the message sender, used for multi-participant conversations.</param>
public sealed record ChatMessage(string Role, string Content, string? Name = null)
{
    /// <summary>Role value for system messages that set context or instructions.</summary>
    public const string SystemRole = "system";

    /// <summary>Role value for user messages.</summary>
    public const string UserRole = "user";

    /// <summary>Role value for assistant (model) responses.</summary>
    public const string AssistantRole = "assistant";

    /// <summary>
    /// Creates a system message with the specified content.
    /// </summary>
    /// <param name="content">The system message content.</param>
    /// <returns>A new <see cref="ChatMessage"/> with the system role.</returns>
    public static ChatMessage System(string content) => new(SystemRole, content);

    /// <summary>
    /// Creates a user message with the specified content.
    /// </summary>
    /// <param name="content">The user message content.</param>
    /// <returns>A new <see cref="ChatMessage"/> with the user role.</returns>
    public static ChatMessage User(string content) => new(UserRole, content);

    /// <summary>
    /// Creates an assistant message with the specified content.
    /// </summary>
    /// <param name="content">The assistant message content.</param>
    /// <returns>A new <see cref="ChatMessage"/> with the assistant role.</returns>
    public static ChatMessage Assistant(string content) => new(AssistantRole, content);
}

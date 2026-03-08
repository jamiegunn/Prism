using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.StreamChat;

/// <summary>
/// Command to stream a chat response in the playground.
/// Creates or continues a conversation with the specified parameters.
/// </summary>
/// <param name="ConversationId">The existing conversation ID to continue, or null to start a new conversation.</param>
/// <param name="InstanceId">The inference provider instance to use.</param>
/// <param name="SystemPrompt">An optional system prompt for the conversation.</param>
/// <param name="UserMessage">The user message to send.</param>
/// <param name="Parameters">The inference parameters for generation.</param>
public sealed record StreamChatCommand(
    Guid? ConversationId,
    Guid InstanceId,
    string? SystemPrompt,
    string UserMessage,
    ConversationParameters Parameters);

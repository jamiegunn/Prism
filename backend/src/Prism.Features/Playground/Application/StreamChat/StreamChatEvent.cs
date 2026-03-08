using Prism.Features.Playground.Application.Dtos;

namespace Prism.Features.Playground.Application.StreamChat;

/// <summary>
/// Base class for server-sent events emitted during a streaming chat operation.
/// </summary>
public abstract record StreamChatEvent;

/// <summary>
/// Emitted when the streaming chat begins, providing conversation and message identifiers.
/// </summary>
/// <param name="ConversationId">The conversation ID (new or existing).</param>
/// <param name="MessageId">The ID assigned to the assistant message being generated.</param>
public sealed record ChatStarted(Guid ConversationId, Guid MessageId) : StreamChatEvent;

/// <summary>
/// Emitted for each token received from the inference provider during streaming.
/// </summary>
/// <param name="Content">The token text content.</param>
/// <param name="Logprob">Optional log probability information for this token.</param>
public sealed record ChatTokenReceived(string Content, TokenLogprobInfo? Logprob) : StreamChatEvent;

/// <summary>
/// Emitted when the streaming chat completes successfully, providing the final message and conversation state.
/// </summary>
/// <param name="Message">The complete assistant message DTO with metrics.</param>
/// <param name="Conversation">The updated conversation DTO.</param>
public sealed record ChatCompleted(MessageDto Message, ConversationDto Conversation) : StreamChatEvent;

/// <summary>
/// Emitted when an error occurs during streaming.
/// </summary>
/// <param name="Error">The error message describing what went wrong.</param>
public sealed record ChatError(string Error) : StreamChatEvent;

/// <summary>
/// Lightweight log probability information for a single streamed token.
/// </summary>
/// <param name="Token">The token text.</param>
/// <param name="Logprob">The log probability value.</param>
/// <param name="Probability">The linear probability (exp of logprob).</param>
/// <param name="TopAlternatives">The top alternative tokens and their probabilities.</param>
public sealed record TokenLogprobInfo(
    string Token,
    double Logprob,
    double Probability,
    List<TokenAlternative> TopAlternatives);

/// <summary>
/// Represents an alternative token considered at a given position.
/// </summary>
/// <param name="Token">The alternative token text.</param>
/// <param name="Logprob">The log probability of this alternative.</param>
/// <param name="Probability">The linear probability of this alternative.</param>
public sealed record TokenAlternative(string Token, double Logprob, double Probability);

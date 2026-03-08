using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.Dtos;

/// <summary>
/// Lightweight data transfer object for conversation list views.
/// Omits message details to reduce payload size.
/// </summary>
/// <param name="Id">The unique conversation identifier.</param>
/// <param name="Title">The conversation title.</param>
/// <param name="ModelId">The model identifier used for this conversation.</param>
/// <param name="MessageCount">The number of messages in the conversation.</param>
/// <param name="TotalTokens">The running total of tokens used.</param>
/// <param name="LastMessageAt">The UTC timestamp of the last message.</param>
/// <param name="IsPinned">Whether the conversation is pinned.</param>
/// <param name="CreatedAt">The UTC timestamp when the conversation was created.</param>
public sealed record ConversationSummaryDto(
    Guid Id,
    string Title,
    string ModelId,
    int MessageCount,
    int TotalTokens,
    DateTime? LastMessageAt,
    bool IsPinned,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a <see cref="ConversationSummaryDto"/> from a <see cref="Conversation"/> entity.
    /// </summary>
    /// <param name="entity">The conversation entity to map.</param>
    /// <param name="messageCount">The number of messages in the conversation.</param>
    /// <returns>A new <see cref="ConversationSummaryDto"/> instance.</returns>
    public static ConversationSummaryDto FromEntity(Conversation entity, int messageCount)
    {
        return new ConversationSummaryDto(
            entity.Id,
            entity.Title,
            entity.ModelId,
            messageCount,
            entity.TotalTokens,
            entity.LastMessageAt,
            entity.IsPinned,
            entity.CreatedAt);
    }
}

using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.Dtos;

/// <summary>
/// Data transfer object representing a full playground conversation with all messages.
/// </summary>
/// <param name="Id">The unique conversation identifier.</param>
/// <param name="Title">The conversation title.</param>
/// <param name="SystemPrompt">The optional system prompt.</param>
/// <param name="ModelId">The model identifier used for this conversation.</param>
/// <param name="InstanceId">The inference provider instance identifier.</param>
/// <param name="Parameters">The inference parameters for this conversation.</param>
/// <param name="Messages">The ordered list of messages in this conversation.</param>
/// <param name="IsPinned">Whether the conversation is pinned.</param>
/// <param name="TotalTokens">The running total of tokens used.</param>
/// <param name="LastMessageAt">The UTC timestamp of the last message.</param>
/// <param name="CreatedAt">The UTC timestamp when the conversation was created.</param>
public sealed record ConversationDto(
    Guid Id,
    string Title,
    string? SystemPrompt,
    string ModelId,
    Guid InstanceId,
    ConversationParameters Parameters,
    List<MessageDto> Messages,
    bool IsPinned,
    int TotalTokens,
    DateTime? LastMessageAt,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a <see cref="ConversationDto"/> from a <see cref="Conversation"/> entity.
    /// </summary>
    /// <param name="entity">The conversation entity to map.</param>
    /// <param name="includeLogprobs">Whether to include logprobs data in message DTOs.</param>
    /// <returns>A new <see cref="ConversationDto"/> instance.</returns>
    public static ConversationDto FromEntity(Conversation entity, bool includeLogprobs = true)
    {
        List<MessageDto> messages = entity.Messages
            .OrderBy(m => m.SortOrder)
            .Select(m => MessageDto.FromEntity(m, includeLogprobs))
            .ToList();

        return new ConversationDto(
            entity.Id,
            entity.Title,
            entity.SystemPrompt,
            entity.ModelId,
            entity.InstanceId,
            entity.Parameters,
            messages,
            entity.IsPinned,
            entity.TotalTokens,
            entity.LastMessageAt,
            entity.CreatedAt);
    }
}

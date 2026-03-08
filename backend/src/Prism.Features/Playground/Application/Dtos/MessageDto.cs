using Prism.Common.Inference.Models;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.Dtos;

/// <summary>
/// Data transfer object representing a message in a playground conversation.
/// </summary>
/// <param name="Id">The unique message identifier.</param>
/// <param name="ConversationId">The parent conversation identifier.</param>
/// <param name="Role">The role of the message sender.</param>
/// <param name="Content">The text content of the message.</param>
/// <param name="TokenCount">The number of tokens in this message.</param>
/// <param name="LogprobsData">The deserialized log probabilities data, if available.</param>
/// <param name="Perplexity">The calculated perplexity of the response.</param>
/// <param name="LatencyMs">The total latency in milliseconds.</param>
/// <param name="TtftMs">The time to first token in milliseconds.</param>
/// <param name="TokensPerSecond">The generation throughput in tokens per second.</param>
/// <param name="FinishReason">The reason generation stopped.</param>
/// <param name="SortOrder">The ordering index within the conversation.</param>
/// <param name="CreatedAt">The UTC timestamp when the message was created.</param>
public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    MessageRole Role,
    string Content,
    int? TokenCount,
    LogprobsData? LogprobsData,
    double? Perplexity,
    int? LatencyMs,
    int? TtftMs,
    double? TokensPerSecond,
    string? FinishReason,
    int SortOrder,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a <see cref="MessageDto"/> from a <see cref="Message"/> entity.
    /// </summary>
    /// <param name="entity">The message entity to map.</param>
    /// <param name="includeLogprobs">Whether to deserialize and include logprobs data.</param>
    /// <returns>A new <see cref="MessageDto"/> instance.</returns>
    public static MessageDto FromEntity(Message entity, bool includeLogprobs = true)
    {
        LogprobsData? logprobs = null;
        if (includeLogprobs && entity.LogprobsJson is not null)
        {
            logprobs = JsonSerializer.Deserialize<LogprobsData>(entity.LogprobsJson);
        }

        return new MessageDto(
            entity.Id,
            entity.ConversationId,
            entity.Role,
            entity.Content,
            entity.TokenCount,
            logprobs,
            entity.Perplexity,
            entity.LatencyMs,
            entity.TtftMs,
            entity.TokensPerSecond,
            entity.FinishReason,
            entity.SortOrder,
            entity.CreatedAt);
    }
}

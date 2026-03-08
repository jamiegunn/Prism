namespace Prism.Features.Playground.Api.Requests;

/// <summary>
/// Request body for sending a message in the playground chat via SSE streaming.
/// </summary>
/// <param name="ConversationId">The existing conversation ID to continue, or null to start a new one.</param>
/// <param name="InstanceId">The inference provider instance ID to use.</param>
/// <param name="SystemPrompt">An optional system prompt for new conversations.</param>
/// <param name="Message">The user message text to send.</param>
/// <param name="Temperature">The sampling temperature (0.0 to 2.0).</param>
/// <param name="TopP">The nucleus sampling parameter.</param>
/// <param name="TopK">The top-K sampling parameter.</param>
/// <param name="MaxTokens">The maximum number of tokens to generate.</param>
/// <param name="StopSequences">Stop sequences that halt generation.</param>
/// <param name="FrequencyPenalty">The frequency penalty (-2.0 to 2.0).</param>
/// <param name="PresencePenalty">The presence penalty (-2.0 to 2.0).</param>
/// <param name="Logprobs">Whether to return log probabilities.</param>
/// <param name="TopLogprobs">The number of top log probabilities to return per token.</param>
public sealed record SendMessageRequest(
    Guid? ConversationId,
    Guid InstanceId,
    string? SystemPrompt,
    string Message,
    double? Temperature,
    double? TopP,
    int? TopK,
    int? MaxTokens,
    List<string>? StopSequences,
    double? FrequencyPenalty,
    double? PresencePenalty,
    bool Logprobs = false,
    int? TopLogprobs = null);

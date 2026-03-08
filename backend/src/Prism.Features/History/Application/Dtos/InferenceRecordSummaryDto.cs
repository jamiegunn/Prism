namespace Prism.Features.History.Application.Dtos;

/// <summary>
/// Summary view of an inference record for list displays.
/// Contains key metrics and truncated previews of request/response content.
/// </summary>
/// <param name="Id">The unique identifier of the record.</param>
/// <param name="SourceModule">The module that originated the inference request.</param>
/// <param name="Model">The model identifier used for inference.</param>
/// <param name="ProviderName">The display name of the provider.</param>
/// <param name="PromptPreview">The first 100 characters of the first user message.</param>
/// <param name="ResponsePreview">The first 100 characters of the response content, or null on failure.</param>
/// <param name="PromptTokens">The number of tokens in the prompt.</param>
/// <param name="CompletionTokens">The number of tokens in the completion.</param>
/// <param name="LatencyMs">The total request latency in milliseconds.</param>
/// <param name="IsSuccess">Whether the request completed successfully.</param>
/// <param name="Tags">The user-defined tags on this record.</param>
/// <param name="StartedAt">The UTC timestamp when the request started.</param>
public sealed record InferenceRecordSummaryDto(
    Guid Id,
    string SourceModule,
    string Model,
    string ProviderName,
    string PromptPreview,
    string? ResponsePreview,
    int PromptTokens,
    int CompletionTokens,
    long LatencyMs,
    bool IsSuccess,
    List<string> Tags,
    DateTime StartedAt);

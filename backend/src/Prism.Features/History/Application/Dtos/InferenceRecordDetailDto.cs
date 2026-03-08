namespace Prism.Features.History.Application.Dtos;

/// <summary>
/// Detailed view of an inference record including full serialized request/response JSON.
/// Used for individual record inspection and replay operations.
/// </summary>
/// <param name="Id">The unique identifier of the record.</param>
/// <param name="SourceModule">The module that originated the inference request.</param>
/// <param name="Model">The model identifier used for inference.</param>
/// <param name="ProviderName">The display name of the provider.</param>
/// <param name="ProviderEndpoint">The endpoint URL of the provider.</param>
/// <param name="ProviderType">The type of inference provider as a string.</param>
/// <param name="RequestJson">The full serialized ChatRequest JSON.</param>
/// <param name="ResponseJson">The full serialized ChatResponse JSON, or null on failure.</param>
/// <param name="PromptTokens">The number of tokens in the prompt.</param>
/// <param name="CompletionTokens">The number of tokens in the completion.</param>
/// <param name="TotalTokens">The total number of tokens used.</param>
/// <param name="LatencyMs">The total request latency in milliseconds.</param>
/// <param name="TtftMs">The time to first token in milliseconds, if available.</param>
/// <param name="Perplexity">The perplexity score, if computed.</param>
/// <param name="IsSuccess">Whether the request completed successfully.</param>
/// <param name="ErrorMessage">The error message on failure, or null on success.</param>
/// <param name="Tags">The user-defined tags on this record.</param>
/// <param name="StartedAt">The UTC timestamp when the request started.</param>
/// <param name="CompletedAt">The UTC timestamp when the request completed.</param>
/// <param name="EnvironmentJson">The serialized EnvironmentSnapshot JSON, or null.</param>
public sealed record InferenceRecordDetailDto(
    Guid Id,
    string SourceModule,
    string Model,
    string ProviderName,
    string ProviderEndpoint,
    string ProviderType,
    string RequestJson,
    string? ResponseJson,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    long LatencyMs,
    int? TtftMs,
    double? Perplexity,
    bool IsSuccess,
    string? ErrorMessage,
    List<string> Tags,
    DateTime StartedAt,
    DateTime CompletedAt,
    string? EnvironmentJson);

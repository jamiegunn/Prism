using Prism.Common.Inference.Models;

namespace Prism.Common.Inference;

/// <summary>
/// Internal record capturing all data needed to persist an inference call to history.
/// Published to a Channel by <see cref="RecordingInferenceProvider"/> for async persistence.
/// </summary>
/// <param name="Id">The unique identifier for this inference record.</param>
/// <param name="Request">The original chat request.</param>
/// <param name="Response">The chat response, or null if the request failed.</param>
/// <param name="ProviderName">The name of the provider that handled the request.</param>
/// <param name="ProviderType">The type of the inference provider.</param>
/// <param name="Endpoint">The provider endpoint URL.</param>
/// <param name="SourceModule">The module that originated this request.</param>
/// <param name="LatencyMs">The total request latency in milliseconds.</param>
/// <param name="StartedAt">The UTC timestamp when the request started.</param>
/// <param name="CompletedAt">The UTC timestamp when the request completed.</param>
/// <param name="IsSuccess">Whether the request completed successfully.</param>
/// <param name="ErrorMessage">The error message if the request failed.</param>
/// <param name="Environment">The environment snapshot captured at the time of the request.</param>
public sealed record InferenceRecordData(
    Guid Id,
    ChatRequest Request,
    ChatResponse? Response,
    string ProviderName,
    InferenceProviderType ProviderType,
    string Endpoint,
    string? SourceModule,
    long LatencyMs,
    DateTime StartedAt,
    DateTime CompletedAt,
    bool IsSuccess,
    string? ErrorMessage,
    EnvironmentSnapshot? Environment);

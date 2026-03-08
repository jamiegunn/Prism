namespace Prism.Features.PromptLab.Application.Dtos;

/// <summary>
/// Data transfer object for the result of testing a prompt template.
/// </summary>
/// <param name="Output">The generated output text.</param>
/// <param name="RenderedPrompt">The user prompt after variable substitution.</param>
/// <param name="ModelId">The model that generated the response.</param>
/// <param name="PromptTokens">The number of prompt tokens consumed.</param>
/// <param name="CompletionTokens">The number of completion tokens generated.</param>
/// <param name="TotalTokens">The total tokens used.</param>
/// <param name="LatencyMs">The total latency in milliseconds.</param>
/// <param name="TtftMs">The time to first token in milliseconds.</param>
/// <param name="TokensPerSecond">The generation rate in tokens per second.</param>
/// <param name="FinishReason">The reason generation finished.</param>
/// <param name="RunId">The ID of the created run, if the result was saved to an experiment.</param>
public sealed record TestPromptResultDto(
    string Output,
    string RenderedPrompt,
    string ModelId,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    long LatencyMs,
    long? TtftMs,
    double? TokensPerSecond,
    string? FinishReason,
    Guid? RunId);

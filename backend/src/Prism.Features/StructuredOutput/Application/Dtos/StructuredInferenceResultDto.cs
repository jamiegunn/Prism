namespace Prism.Features.StructuredOutput.Application.Dtos;

/// <summary>
/// Result of structured output inference with guided decoding.
/// </summary>
public sealed record StructuredInferenceResultDto(
    string RawOutput,
    object? ParsedJson,
    bool IsValid,
    List<string> ValidationErrors,
    int PromptTokens,
    int CompletionTokens,
    double LatencyMs);

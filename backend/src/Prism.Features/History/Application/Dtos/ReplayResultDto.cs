namespace Prism.Features.History.Application.Dtos;

/// <summary>
/// Contains the result of replaying an inference record against a (potentially different) provider instance.
/// Includes the original record details and the new response for side-by-side comparison.
/// </summary>
/// <param name="OriginalRecordId">The unique identifier of the original inference record.</param>
/// <param name="Original">The full detail DTO of the original record.</param>
/// <param name="ReplayResponseContent">The text content returned by the replay inference call.</param>
/// <param name="ReplayPromptTokens">The number of prompt tokens used in the replay call.</param>
/// <param name="ReplayCompletionTokens">The number of completion tokens generated in the replay call.</param>
/// <param name="ReplayLatencyMs">The total latency of the replay call in milliseconds.</param>
/// <param name="ReplayModel">The model identifier used for the replay call.</param>
/// <param name="DiffSummary">A simple textual summary of differences between original and replay responses.</param>
public sealed record ReplayResultDto(
    Guid OriginalRecordId,
    InferenceRecordDetailDto Original,
    string ReplayResponseContent,
    int ReplayPromptTokens,
    int ReplayCompletionTokens,
    long ReplayLatencyMs,
    string ReplayModel,
    string? DiffSummary);

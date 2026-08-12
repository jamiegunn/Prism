namespace Prism.Features.History.Application.Dtos;

/// <summary>
/// Contains the result of replaying an inference record against a (potentially different) provider instance.
/// Carries both responses as text so a client can put them side by side.
/// </summary>
/// <remarks>
/// The original arrives as its response <em>content</em> rather than the whole record. The record
/// is already on the screen that starts a replay, and its serialized response carries every token's
/// logprobs — tens of kilobytes the comparison never reads. Sending the content is also what the
/// diff is computed over, so client and server describe the same two strings.
/// </remarks>
/// <param name="OriginalRecordId">The unique identifier of the original inference record.</param>
/// <param name="OriginalResponseContent">
/// The text the original call returned, or null when it failed before producing one.
/// </param>
/// <param name="ReplayResponseContent">The text content returned by the replay inference call.</param>
/// <param name="ReplayPromptTokens">The number of prompt tokens used in the replay call.</param>
/// <param name="ReplayCompletionTokens">The number of completion tokens generated in the replay call.</param>
/// <param name="ReplayLatencyMs">The total latency of the replay call in milliseconds.</param>
/// <param name="ReplayModel">The model identifier used for the replay call.</param>
/// <param name="DiffSummary">
/// A textual summary of the differences between original and replay responses. Always present:
/// there is always something true to say about two strings, including that one of them is absent.
/// </param>
public sealed record ReplayResultDto(
    Guid OriginalRecordId,
    string? OriginalResponseContent,
    string ReplayResponseContent,
    int ReplayPromptTokens,
    int ReplayCompletionTokens,
    long ReplayLatencyMs,
    string ReplayModel,
    string DiffSummary);

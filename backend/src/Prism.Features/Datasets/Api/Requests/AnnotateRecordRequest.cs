namespace Prism.Features.Datasets.Api.Requests;

/// <summary>
/// Request body for annotating a dataset record.
/// </summary>
/// <param name="Label">Optional annotation label (e.g., "correct", "incorrect", "needs-review").</param>
/// <param name="Note">Optional annotation note.</param>
/// <param name="IsCorrect">Optional correctness flag.</param>
public sealed record AnnotateRecordRequest(
    string? Label = null,
    string? Note = null,
    bool? IsCorrect = null);

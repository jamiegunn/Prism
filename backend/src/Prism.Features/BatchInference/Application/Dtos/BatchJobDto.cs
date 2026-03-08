using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Application.Dtos;

/// <summary>
/// Data transfer object for a batch inference job.
/// </summary>
public sealed record BatchJobDto(
    Guid Id,
    Guid DatasetId,
    string? SplitLabel,
    string Model,
    Guid? PromptVersionId,
    Dictionary<string, object?> Parameters,
    int Concurrency,
    int MaxRetries,
    bool CaptureLogprobs,
    string Status,
    double Progress,
    int TotalRecords,
    int CompletedRecords,
    int FailedRecords,
    long TokensUsed,
    decimal? Cost,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    public static BatchJobDto FromEntity(BatchJob e) => new(
        e.Id, e.DatasetId, e.SplitLabel, e.Model, e.PromptVersionId,
        e.Parameters, e.Concurrency, e.MaxRetries, e.CaptureLogprobs,
        e.Status.ToString(), e.Progress, e.TotalRecords, e.CompletedRecords, e.FailedRecords,
        e.TokensUsed, e.Cost, e.StartedAt, e.FinishedAt, e.ErrorMessage,
        e.CreatedAt, e.UpdatedAt);
}

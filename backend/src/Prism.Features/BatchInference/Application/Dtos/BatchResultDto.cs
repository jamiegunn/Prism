using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Application.Dtos;

/// <summary>
/// Data transfer object for a batch inference result.
/// </summary>
public sealed record BatchResultDto(
    Guid Id,
    Guid BatchJobId,
    Guid RecordId,
    string Input,
    string? Output,
    string? LogprobsData,
    double? Perplexity,
    int TokensUsed,
    long LatencyMs,
    string Status,
    string? Error,
    int Attempt,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    public static BatchResultDto FromEntity(BatchResult r) => new(
        r.Id, r.BatchJobId, r.RecordId,
        r.Input, r.Output, r.LogprobsData, r.Perplexity,
        r.TokensUsed, r.LatencyMs, r.Status.ToString(),
        r.Error, r.Attempt, r.CreatedAt);
}

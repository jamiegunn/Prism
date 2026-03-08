using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.Dtos;

/// <summary>
/// Data transfer object for an evaluation result.
/// </summary>
public sealed record EvaluationResultDto(
    Guid Id,
    Guid EvaluationId,
    string Model,
    Guid RecordId,
    string Input,
    string? ExpectedOutput,
    string? ActualOutput,
    Dictionary<string, double> Scores,
    string? LogprobsData,
    double? Perplexity,
    long LatencyMs,
    int PromptTokens,
    int CompletionTokens,
    string? Error,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    /// <param name="r">The evaluation result entity.</param>
    /// <returns>A new <see cref="EvaluationResultDto"/>.</returns>
    public static EvaluationResultDto FromEntity(EvaluationResult r) => new(
        r.Id, r.EvaluationId, r.Model, r.RecordId,
        r.Input, r.ExpectedOutput, r.ActualOutput,
        r.Scores, r.LogprobsData, r.Perplexity,
        r.LatencyMs, r.PromptTokens, r.CompletionTokens,
        r.Error, r.CreatedAt);
}

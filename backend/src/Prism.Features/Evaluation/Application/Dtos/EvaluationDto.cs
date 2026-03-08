using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.Dtos;

/// <summary>
/// Data transfer object for an evaluation.
/// </summary>
public sealed record EvaluationDto(
    Guid Id,
    Guid? ProjectId,
    Guid DatasetId,
    string? SplitLabel,
    string Name,
    List<string> Models,
    Guid? PromptVersionId,
    List<string> ScoringMethods,
    Dictionary<string, object?> Config,
    string Status,
    double Progress,
    int TotalRecords,
    int CompletedRecords,
    int FailedRecords,
    string? ErrorMessage,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    /// <param name="e">The evaluation entity.</param>
    /// <returns>A new <see cref="EvaluationDto"/>.</returns>
    public static EvaluationDto FromEntity(EvaluationEntity e) => new(
        e.Id, e.ProjectId, e.DatasetId, e.SplitLabel, e.Name,
        e.Models, e.PromptVersionId, e.ScoringMethods, e.Config,
        e.Status.ToString(), e.Progress, e.TotalRecords, e.CompletedRecords, e.FailedRecords,
        e.ErrorMessage, e.StartedAt, e.FinishedAt, e.CreatedAt, e.UpdatedAt);
}

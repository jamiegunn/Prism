namespace Prism.Features.Evaluation.Api.Requests;

/// <summary>
/// HTTP request body for starting a new evaluation.
/// </summary>
public sealed record StartEvaluationRequest(
    string Name,
    Guid DatasetId,
    string? SplitLabel,
    Guid? ProjectId,
    List<string> Models,
    Guid? PromptVersionId,
    List<string> ScoringMethods,
    Dictionary<string, object?>? Config);

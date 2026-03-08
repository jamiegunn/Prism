namespace Prism.Features.PromptLab.Application.Dtos;

/// <summary>
/// Data transfer object for the result of starting an A/B test.
/// </summary>
/// <param name="ExperimentId">The ID of the created experiment.</param>
/// <param name="TotalCombinations">The total number of combinations to execute.</param>
/// <param name="Status">The current status (queued for background execution).</param>
public sealed record AbTestResultDto(
    Guid ExperimentId,
    int TotalCombinations,
    string Status);

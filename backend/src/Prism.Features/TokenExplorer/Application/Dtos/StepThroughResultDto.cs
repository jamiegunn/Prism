namespace Prism.Features.TokenExplorer.Application.Dtos;

/// <summary>
/// Contains the result of a step-through operation: the extended text and predictions for the next position.
/// </summary>
/// <param name="ExtendedText">The full prompt text after appending the selected token.</param>
/// <param name="AppendedToken">The token that was appended to the prompt.</param>
/// <param name="NextPredictions">The next-token predictions from the extended prompt position.</param>
public sealed record StepThroughResultDto(
    string ExtendedText,
    string AppendedToken,
    NextTokenPredictionDto NextPredictions);

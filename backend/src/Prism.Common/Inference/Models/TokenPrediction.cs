namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents the result of a next-token prediction, containing the most likely continuations.
/// </summary>
/// <param name="Predictions">The list of most likely next tokens with their probabilities.</param>
/// <param name="InputTokenCount">The number of tokens in the input that was analyzed.</param>
/// <param name="ModelId">The model that generated the predictions.</param>
public sealed record TokenPrediction(
    IReadOnlyList<TopLogprob> Predictions,
    int InputTokenCount,
    string ModelId);

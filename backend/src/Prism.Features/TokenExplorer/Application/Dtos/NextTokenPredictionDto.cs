namespace Prism.Features.TokenExplorer.Application.Dtos;

/// <summary>
/// Contains the predicted next tokens with their probabilities, returned by the predict endpoint.
/// </summary>
/// <param name="Predictions">The ranked list of token predictions with probabilities.</param>
/// <param name="InputTokenCount">The number of tokens in the input prompt.</param>
/// <param name="ModelId">The identifier of the model that produced the predictions.</param>
/// <param name="TotalProbability">The sum of all returned token probabilities (for top-p visualization).</param>
public sealed record NextTokenPredictionDto(
    IReadOnlyList<TokenPredictionEntry> Predictions,
    int InputTokenCount,
    string ModelId,
    double TotalProbability);

/// <summary>
/// Represents a single token prediction entry with its probability and cumulative probability.
/// </summary>
/// <param name="Token">The predicted token text.</param>
/// <param name="Logprob">The log probability of this token.</param>
/// <param name="Probability">The linear probability of this token (exp of logprob).</param>
/// <param name="CumulativeProbability">The running sum of probabilities for top-p visualization.</param>
public sealed record TokenPredictionEntry(
    string Token,
    double Logprob,
    double Probability,
    double CumulativeProbability);

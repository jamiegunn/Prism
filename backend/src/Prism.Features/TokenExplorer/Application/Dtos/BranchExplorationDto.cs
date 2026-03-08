namespace Prism.Features.TokenExplorer.Application.Dtos;

/// <summary>
/// Contains the result of exploring an alternative generation branch from a forced starting token.
/// </summary>
/// <param name="ForcedToken">The token that was forced as the start of the branch.</param>
/// <param name="GeneratedText">The full generated text including the forced token.</param>
/// <param name="Tokens">Token-by-token logprob data for the generated sequence.</param>
/// <param name="Perplexity">The perplexity of the generated sequence, or null if no logprobs were returned.</param>
/// <param name="ModelId">The identifier of the model that generated the branch.</param>
public sealed record BranchExplorationDto(
    string ForcedToken,
    string GeneratedText,
    IReadOnlyList<BranchTokenDto> Tokens,
    double? Perplexity,
    string ModelId);

/// <summary>
/// Represents a single token in a branch exploration with its logprob data and top alternatives.
/// </summary>
/// <param name="Token">The token text.</param>
/// <param name="Logprob">The log probability of this token.</param>
/// <param name="Probability">The linear probability of this token.</param>
/// <param name="TopAlternatives">The top alternative tokens at this position.</param>
public sealed record BranchTokenDto(
    string Token,
    double Logprob,
    double Probability,
    IReadOnlyList<TokenAlternativeDto> TopAlternatives);

/// <summary>
/// Represents an alternative token that could have been generated at a given position.
/// </summary>
/// <param name="Token">The alternative token text.</param>
/// <param name="Logprob">The log probability of this alternative token.</param>
/// <param name="Probability">The linear probability of this alternative token.</param>
public sealed record TokenAlternativeDto(
    string Token,
    double Logprob,
    double Probability);

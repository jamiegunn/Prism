namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents the result of tokenizing a text string.
/// </summary>
/// <param name="Tokens">The list of individual tokens with their metadata.</param>
/// <param name="TokenCount">The total number of tokens.</param>
public sealed record TokenizeResponse(IReadOnlyList<TokenInfo> Tokens, int TokenCount);

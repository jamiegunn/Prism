namespace Prism.Features.TokenExplorer.Application.Dtos;

/// <summary>
/// Result of tokenizing text, including per-token details and aggregate statistics.
/// </summary>
/// <param name="Tokens">The list of individual token blocks with display metadata.</param>
/// <param name="TokenCount">The total number of tokens.</param>
/// <param name="CharacterCount">The total number of characters in the input text.</param>
/// <param name="ByteCount">The total UTF-8 byte count of the input text.</param>
/// <param name="ModelId">The model identifier used for tokenization.</param>
public sealed record TokenizeResultDto(
    IReadOnlyList<TokenBlockDto> Tokens,
    int TokenCount,
    int CharacterCount,
    int ByteCount,
    string ModelId);

/// <summary>
/// A single token block with display-friendly text and hex byte representation.
/// </summary>
/// <param name="Id">The numeric token ID in the model's vocabulary.</param>
/// <param name="Text">The raw text of the token.</param>
/// <param name="DisplayText">The display text with visible whitespace characters.</param>
/// <param name="ByteLength">The UTF-8 byte length of the token text.</param>
/// <param name="HexBytes">The hex representation of the token's UTF-8 bytes (e.g., "48 65 6C 6C 6F").</param>
public sealed record TokenBlockDto(
    int Id,
    string Text,
    string DisplayText,
    int ByteLength,
    string HexBytes);

/// <summary>
/// Result of comparing tokenization across multiple inference instances.
/// </summary>
/// <param name="Text">The input text that was tokenized.</param>
/// <param name="Results">The tokenization results per instance.</param>
public sealed record CompareTokenizeResultDto(
    string Text,
    IReadOnlyList<InstanceTokenizeResult> Results);

/// <summary>
/// Tokenization result for a single inference instance in a comparison operation.
/// </summary>
/// <param name="InstanceId">The inference instance ID.</param>
/// <param name="InstanceName">The display name of the inference instance.</param>
/// <param name="ModelId">The model identifier used by this instance.</param>
/// <param name="Tokenization">The tokenization result, or null if tokenization failed.</param>
/// <param name="Error">The error message if tokenization failed, or null on success.</param>
public sealed record InstanceTokenizeResult(
    Guid InstanceId,
    string InstanceName,
    string ModelId,
    TokenizeResultDto? Tokenization,
    string? Error);

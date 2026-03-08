namespace Prism.Features.Models.Application.Dtos;

/// <summary>
/// Tokenizer information for an inference instance.
/// </summary>
/// <param name="VocabSize">The total vocabulary size, or null if unknown.</param>
/// <param name="TokenizerType">The tokenizer family (e.g., "BPE", "SentencePiece"), or null if unknown.</param>
/// <param name="SpecialTokens">A dictionary of special token names to their string representations.</param>
/// <param name="ModelId">The model identifier this tokenizer belongs to.</param>
public sealed record TokenizerInfoDto(
    int? VocabSize,
    string? TokenizerType,
    Dictionary<string, string> SpecialTokens,
    string ModelId);

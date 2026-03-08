namespace Prism.Common.Inference.Models;

/// <summary>
/// Information about a model's tokenizer, including vocabulary size and special tokens.
/// </summary>
/// <param name="VocabSize">The total vocabulary size, or null if unknown.</param>
/// <param name="TokenizerType">The tokenizer family (e.g., "BPE", "SentencePiece"), or null if unknown.</param>
/// <param name="SpecialTokens">A dictionary of special token names to their string representations.</param>
/// <param name="ModelId">The model identifier this tokenizer belongs to.</param>
public sealed record TokenizerInfo(
    int? VocabSize,
    string? TokenizerType,
    IReadOnlyDictionary<string, string> SpecialTokens,
    string ModelId);

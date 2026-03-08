namespace Prism.Features.TokenExplorer.Application.Dtos;

/// <summary>
/// Represents the result of decoding token IDs back to text.
/// </summary>
/// <param name="Text">The decoded text.</param>
/// <param name="TokenIds">The original token IDs that were decoded.</param>
/// <param name="ModelId">The model whose tokenizer was used.</param>
public sealed record DetokenizeResultDto(string Text, IReadOnlyList<int> TokenIds, string ModelId);

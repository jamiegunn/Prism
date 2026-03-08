namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents a single token from tokenization output.
/// </summary>
/// <param name="Id">The numeric token ID in the model's vocabulary.</param>
/// <param name="Text">The text representation of the token.</param>
/// <param name="ByteLength">The byte length of the token's text representation.</param>
public sealed record TokenInfo(int Id, string Text, int ByteLength);

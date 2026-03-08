namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents the result of detokenizing a list of token IDs back to text.
/// </summary>
/// <param name="Text">The decoded text produced from the token IDs.</param>
public sealed record DetokenizeResponse(string Text);

namespace Prism.Features.TokenExplorer.Application.Tokenize;

/// <summary>
/// Command to tokenize the same text across multiple inference instances for comparison.
/// </summary>
/// <param name="InstanceIds">The inference instance IDs to tokenize with.</param>
/// <param name="Text">The text to tokenize across all instances.</param>
public sealed record CompareTokenizeCommand(IReadOnlyList<Guid> InstanceIds, string Text);

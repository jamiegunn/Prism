namespace Prism.Features.TokenExplorer.Application.Tokenize;

/// <summary>
/// Command to detokenize a list of token IDs back to text using a specific inference instance.
/// </summary>
/// <param name="InstanceId">The inference instance to use for detokenization.</param>
/// <param name="TokenIds">The token IDs to decode.</param>
public sealed record DetokenizeCommand(Guid InstanceId, IReadOnlyList<int> TokenIds);

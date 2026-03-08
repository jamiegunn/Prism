namespace Prism.Features.TokenExplorer.Application.Tokenize;

/// <summary>
/// Command to tokenize text using a specific inference instance.
/// </summary>
/// <param name="InstanceId">The inference instance to use for tokenization.</param>
/// <param name="Text">The text to tokenize.</param>
public sealed record TokenizeCommand(Guid InstanceId, string Text);

namespace Prism.Features.TokenExplorer.Api.Requests;

/// <summary>
/// HTTP request body for the tokenize endpoint.
/// </summary>
/// <param name="InstanceId">The inference instance to use for tokenization.</param>
/// <param name="Text">The text to tokenize.</param>
public sealed record TokenizeRequest(Guid InstanceId, string Text);

namespace Prism.Features.TokenExplorer.Api.Requests;

/// <summary>
/// HTTP request body for the compare tokenize endpoint.
/// </summary>
/// <param name="InstanceIds">The inference instance IDs to tokenize with.</param>
/// <param name="Text">The text to tokenize across all instances.</param>
public sealed record CompareTokenizeRequest(IReadOnlyList<Guid> InstanceIds, string Text);

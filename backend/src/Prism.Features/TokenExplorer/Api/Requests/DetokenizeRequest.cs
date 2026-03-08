namespace Prism.Features.TokenExplorer.Api.Requests;

/// <summary>
/// HTTP request body for the detokenize endpoint.
/// </summary>
/// <param name="InstanceId">The inference instance to use for detokenization.</param>
/// <param name="TokenIds">The token IDs to decode back to text.</param>
public sealed record DetokenizeRequest(Guid InstanceId, IReadOnlyList<int> TokenIds);

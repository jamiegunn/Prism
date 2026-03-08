namespace Prism.Features.Models.Api.Requests;

/// <summary>
/// HTTP request body for swapping the model loaded on an inference instance.
/// </summary>
/// <param name="ModelId">The identifier of the model to load.</param>
public sealed record SwapModelRequest(string ModelId);

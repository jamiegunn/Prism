namespace Prism.Features.Models.Api.Requests;

/// <summary>
/// HTTP request body for registering a new inference provider instance.
/// </summary>
/// <param name="Name">The display name for the instance.</param>
/// <param name="Endpoint">The base endpoint URL of the inference provider.</param>
/// <param name="ProviderType">The type of inference provider (e.g., "Vllm", "Ollama", "LmStudio", "OpenAiCompatible").</param>
/// <param name="IsDefault">Whether this should be the default instance. Defaults to false.</param>
/// <param name="Tags">Optional user-defined tags for categorizing the instance.</param>
public sealed record RegisterInstanceRequest(
    string Name,
    string Endpoint,
    string ProviderType,
    bool? IsDefault = null,
    List<string>? Tags = null);

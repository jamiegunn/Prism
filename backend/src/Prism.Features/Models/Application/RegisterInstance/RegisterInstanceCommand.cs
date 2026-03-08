using Prism.Common.Inference;

namespace Prism.Features.Models.Application.RegisterInstance;

/// <summary>
/// Command to register a new inference provider instance.
/// </summary>
/// <param name="Name">The display name for the instance.</param>
/// <param name="Endpoint">The base endpoint URL of the inference provider.</param>
/// <param name="ProviderType">The type of inference provider.</param>
/// <param name="IsDefault">Whether this should be the default instance for inference requests.</param>
/// <param name="Tags">Optional user-defined tags for categorizing the instance.</param>
public sealed record RegisterInstanceCommand(
    string Name,
    string Endpoint,
    InferenceProviderType ProviderType,
    bool IsDefault = false,
    List<string>? Tags = null);

using Prism.Common.Inference;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.Dtos;

/// <summary>
/// Data transfer object representing an inference provider instance.
/// </summary>
/// <param name="Id">The unique identifier of the instance.</param>
/// <param name="Name">The display name of the instance.</param>
/// <param name="Endpoint">The base endpoint URL of the inference provider.</param>
/// <param name="ProviderType">The type of inference provider.</param>
/// <param name="Status">The current operational status.</param>
/// <param name="ModelId">The identifier of the currently loaded model.</param>
/// <param name="GpuConfig">The GPU configuration description.</param>
/// <param name="MaxContextLength">The maximum context length in tokens.</param>
/// <param name="SupportsLogprobs">Whether the provider supports log probabilities.</param>
/// <param name="MaxTopLogprobs">The maximum number of top log probabilities per token.</param>
/// <param name="SupportsStreaming">Whether the provider supports streaming responses.</param>
/// <param name="SupportsMetrics">Whether the provider exposes performance metrics.</param>
/// <param name="SupportsTokenize">Whether the provider supports tokenization.</param>
/// <param name="SupportsGuidedDecoding">Whether the provider supports guided decoding.</param>
/// <param name="SupportsMultimodal">Whether the provider supports multimodal inputs.</param>
/// <param name="SupportsModelSwap">Whether the provider supports hot-swapping models.</param>
/// <param name="IsDefault">Whether this is the default instance.</param>
/// <param name="LastHealthCheck">The UTC timestamp of the last health check.</param>
/// <param name="LastHealthError">The error message from the last failed health check.</param>
/// <param name="Tags">User-defined tags for this instance.</param>
/// <param name="CreatedAt">The UTC timestamp when the instance was created.</param>
/// <param name="UpdatedAt">The UTC timestamp when the instance was last updated.</param>
public sealed record InferenceInstanceDto(
    Guid Id,
    string Name,
    string Endpoint,
    InferenceProviderType ProviderType,
    InstanceStatus Status,
    string? ModelId,
    string? GpuConfig,
    int? MaxContextLength,
    bool SupportsLogprobs,
    int MaxTopLogprobs,
    bool SupportsStreaming,
    bool SupportsMetrics,
    bool SupportsTokenize,
    bool SupportsGuidedDecoding,
    bool SupportsMultimodal,
    bool SupportsModelSwap,
    bool IsDefault,
    DateTime? LastHealthCheck,
    string? LastHealthError,
    List<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates an <see cref="InferenceInstanceDto"/> from an <see cref="InferenceInstance"/> entity.
    /// </summary>
    /// <param name="entity">The inference instance entity to map.</param>
    /// <returns>A new <see cref="InferenceInstanceDto"/> representing the entity.</returns>
    public static InferenceInstanceDto FromEntity(InferenceInstance entity) => new(
        Id: entity.Id,
        Name: entity.Name,
        Endpoint: entity.Endpoint,
        ProviderType: entity.ProviderType,
        Status: entity.Status,
        ModelId: entity.ModelId,
        GpuConfig: entity.GpuConfig,
        MaxContextLength: entity.MaxContextLength,
        SupportsLogprobs: entity.SupportsLogprobs,
        MaxTopLogprobs: entity.MaxTopLogprobs,
        SupportsStreaming: entity.SupportsStreaming,
        SupportsMetrics: entity.SupportsMetrics,
        SupportsTokenize: entity.SupportsTokenize,
        SupportsGuidedDecoding: entity.SupportsGuidedDecoding,
        SupportsMultimodal: entity.SupportsMultimodal,
        SupportsModelSwap: entity.SupportsModelSwap,
        IsDefault: entity.IsDefault,
        LastHealthCheck: entity.LastHealthCheck,
        LastHealthError: entity.LastHealthError,
        Tags: entity.Tags,
        CreatedAt: entity.CreatedAt,
        UpdatedAt: entity.UpdatedAt);
}

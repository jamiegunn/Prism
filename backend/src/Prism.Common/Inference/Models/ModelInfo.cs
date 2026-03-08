namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents metadata about a model loaded in an inference provider.
/// </summary>
/// <param name="ModelId">The unique identifier of the model.</param>
/// <param name="OwnedBy">The optional owner or organization that created the model.</param>
/// <param name="MaxContextLength">The maximum context window size in tokens.</param>
/// <param name="Capabilities">The capabilities supported by this model on this provider.</param>
public sealed record ModelInfo(
    string ModelId,
    string? OwnedBy,
    int MaxContextLength,
    ProviderCapabilities Capabilities);

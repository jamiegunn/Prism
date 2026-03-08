namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents a model available on a provider that can be loaded for inference.
/// Used by <see cref="IHotReloadableProvider"/> to list models that can be loaded/unloaded.
/// </summary>
/// <param name="ModelId">The unique identifier of the model.</param>
/// <param name="Name">The human-readable name of the model, if different from the ID.</param>
/// <param name="Size">The model size in bytes, if known.</param>
/// <param name="QuantizationLevel">The quantization format (e.g., "Q4_K_M", "GPTQ", "AWQ"), if applicable.</param>
/// <param name="Family">The model family (e.g., "llama", "mistral", "qwen"), if known.</param>
public sealed record AvailableModel(
    string ModelId,
    string? Name,
    long? Size,
    string? QuantizationLevel,
    string? Family);

namespace Prism.Common.Inference;

/// <summary>
/// Captures the runtime environment at the time of an inference call.
/// Attached to every inference record for reproducibility and debugging.
/// </summary>
/// <param name="ProviderType">The type of inference provider used.</param>
/// <param name="ProviderVersion">The version of the provider software, if available.</param>
/// <param name="Model">The model identifier used for inference.</param>
/// <param name="GpuInfo">GPU hardware information (e.g., "NVIDIA RTX 4090"), if available.</param>
/// <param name="Quantization">The quantization method applied to the model, if any.</param>
/// <param name="CapturedAt">The UTC timestamp when this snapshot was captured.</param>
public sealed record EnvironmentSnapshot(
    InferenceProviderType ProviderType,
    string? ProviderVersion,
    string Model,
    string? GpuInfo,
    string? Quantization,
    DateTime CapturedAt);

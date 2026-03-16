using Prism.Common.Database;

namespace Prism.Features.FineTuning.Domain;

/// <summary>
/// Represents a registered LoRA adapter for fine-tuned model inference.
/// </summary>
public sealed class LoraAdapter : BaseEntity
{
    /// <summary>
    /// Gets or sets the display name of this adapter.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the description of what this adapter does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the inference instance this adapter is registered with.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the filesystem path to the LoRA adapter weights.
    /// </summary>
    public string AdapterPath { get; set; } = "";

    /// <summary>
    /// Gets or sets the base model this adapter was trained on.
    /// </summary>
    public string BaseModel { get; set; } = "";

    /// <summary>
    /// Gets or sets whether this adapter is currently active/loaded.
    /// </summary>
    public bool IsActive { get; set; }
}

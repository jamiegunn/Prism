using Prism.Common.Database;

namespace Prism.Features.Notebooks.Domain;

/// <summary>
/// Represents a Jupyter notebook stored in the platform.
/// Contains the .ipynb JSON content and metadata for versioning.
/// </summary>
public sealed class Notebook : BaseEntity
{
    /// <summary>
    /// Gets or sets the optional project this notebook belongs to.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the notebook.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the .ipynb JSON content of the notebook.
    /// </summary>
    public string Content { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the version number, incremented on each save.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the size of the content in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the kernel name used by this notebook.
    /// </summary>
    public string KernelName { get; set; } = "python";

    /// <summary>
    /// Gets or sets the timestamp of the last edit.
    /// </summary>
    public DateTime? LastEditedAt { get; set; }
}

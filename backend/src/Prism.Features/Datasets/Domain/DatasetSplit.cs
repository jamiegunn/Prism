using Prism.Common.Database;

namespace Prism.Features.Datasets.Domain;

/// <summary>
/// Represents a named split (e.g., train/test/val) within a dataset.
/// </summary>
public sealed class DatasetSplit : BaseEntity
{
    /// <summary>
    /// Gets or sets the parent dataset identifier.
    /// </summary>
    public Guid DatasetId { get; set; }

    /// <summary>
    /// Gets or sets the split name (e.g., "train", "test", "val").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the number of records in this split.
    /// </summary>
    public int RecordCount { get; set; }
}

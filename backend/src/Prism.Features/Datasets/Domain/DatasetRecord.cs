using Prism.Common.Database;

namespace Prism.Features.Datasets.Domain;

/// <summary>
/// A single record within a dataset. Data is stored as flexible JSONB.
/// </summary>
public sealed class DatasetRecord : BaseEntity
{
    /// <summary>
    /// Gets or sets the parent dataset identifier.
    /// </summary>
    public Guid DatasetId { get; set; }

    /// <summary>
    /// Gets or sets the record data as a JSON dictionary.
    /// Stored as JSONB in PostgreSQL with GIN index for querying.
    /// </summary>
    public Dictionary<string, object?> Data { get; set; } = new();

    /// <summary>
    /// Gets or sets the split label (e.g., "train", "test", "val") or null if not split.
    /// </summary>
    public string? SplitLabel { get; set; }

    /// <summary>
    /// Gets or sets the order index for maintaining record sequence.
    /// </summary>
    public int OrderIndex { get; set; }
}

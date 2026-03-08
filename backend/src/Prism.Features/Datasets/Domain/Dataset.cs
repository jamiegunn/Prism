using Prism.Common.Database;

namespace Prism.Features.Datasets.Domain;

/// <summary>
/// Aggregate root representing a dataset containing records for evaluation or fine-tuning.
/// </summary>
public sealed class Dataset : BaseEntity
{
    /// <summary>
    /// Gets or sets the optional project this dataset belongs to.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the dataset.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the source file format.
    /// </summary>
    public DatasetFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the column schema as a list of column definitions.
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public List<ColumnSchema> Schema { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of records in this dataset.
    /// </summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// Gets or sets the total size of the source file in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the version number, incremented on each modification.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets the records belonging to this dataset.
    /// </summary>
    public List<DatasetRecord> Records { get; set; } = [];

    /// <summary>
    /// Gets the splits defined for this dataset.
    /// </summary>
    public List<DatasetSplit> Splits { get; set; } = [];
}

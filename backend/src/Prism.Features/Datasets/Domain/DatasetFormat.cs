namespace Prism.Features.Datasets.Domain;

/// <summary>
/// Supported dataset file formats for upload and export.
/// </summary>
public enum DatasetFormat
{
    /// <summary>Comma-separated values.</summary>
    Csv,

    /// <summary>JSON array of objects.</summary>
    Json,

    /// <summary>JSON Lines (one JSON object per line).</summary>
    Jsonl,

    /// <summary>Apache Parquet columnar format.</summary>
    Parquet
}

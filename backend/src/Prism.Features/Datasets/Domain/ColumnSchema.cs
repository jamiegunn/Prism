namespace Prism.Features.Datasets.Domain;

/// <summary>
/// Describes a column in a dataset's schema, including its name, type, and mapping purpose.
/// </summary>
public sealed record ColumnSchema
{
    /// <summary>
    /// Gets the column name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the data type of the column (e.g., "string", "number", "boolean", "array", "object").
    /// </summary>
    public string Type { get; init; } = "string";

    /// <summary>
    /// Gets the purpose of this column in evaluation/fine-tuning (e.g., "input", "output", "label", "metadata").
    /// Null if not yet mapped.
    /// </summary>
    public string? Purpose { get; init; }
}

using Prism.Common.Database;

namespace Prism.Features.StructuredOutput.Domain;

/// <summary>
/// Represents a stored JSON schema used for guided decoding and validation.
/// </summary>
public sealed class JsonSchemaEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the optional project this schema belongs to.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the schema.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema definition as a string.
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public string SchemaJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the schema version, incremented on each update.
    /// </summary>
    public int Version { get; set; } = 1;
}

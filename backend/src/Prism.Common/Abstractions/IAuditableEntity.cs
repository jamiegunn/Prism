namespace Prism.Common.Abstractions;

/// <summary>
/// Defines auditing properties for entities that track creation and modification timestamps.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was last updated.
    /// </summary>
    DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who created the entity.
    /// Null for system-generated entities or when auth is disabled.
    /// </summary>
    string? CreatedBy { get; set; }
}

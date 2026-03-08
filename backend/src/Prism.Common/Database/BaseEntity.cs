using Prism.Common.Abstractions;

namespace Prism.Common.Database;

/// <summary>
/// Base class for all domain entities providing identity and audit timestamps.
/// All entities in the system should inherit from this class.
/// </summary>
public abstract class BaseEntity : IAuditableEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the identifier of the user who created this entity.
    /// Null for system-generated entities or when authentication is disabled.
    /// </summary>
    public string? CreatedBy { get; set; }
}

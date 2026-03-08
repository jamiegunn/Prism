using Prism.Common.Database;

namespace Prism.Features.PromptLab.Domain;

/// <summary>
/// Aggregate root representing a reusable prompt template with versioned content.
/// </summary>
public sealed class PromptTemplate : BaseEntity
{
    /// <summary>
    /// Gets or sets the optional project ID this template is associated with.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the template.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional category for organizing templates.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the template.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the tags for filtering and categorization.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the latest version number for this template.
    /// </summary>
    public int LatestVersion { get; set; }

    /// <summary>
    /// Gets or sets the versions belonging to this template.
    /// </summary>
    public List<PromptVersion> Versions { get; set; } = [];
}

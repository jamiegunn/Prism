using Prism.Common.Database;

namespace Prism.Features.Workspaces.Domain;

/// <summary>
/// Top-level organizational container for grouping projects.
/// All user work (projects, experiments, datasets, etc.) lives within a workspace.
/// Single-user mode creates a default workspace automatically.
/// </summary>
public sealed class Workspace : BaseEntity
{
    /// <summary>
    /// Gets or sets the display name of the workspace.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this is the default workspace (auto-created on first launch).
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the optional icon or color identifier for the workspace.
    /// </summary>
    public string? IconColor { get; set; }
}

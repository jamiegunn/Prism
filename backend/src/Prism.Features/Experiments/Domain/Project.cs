using Prism.Common.Database;

namespace Prism.Features.Experiments.Domain;

/// <summary>
/// Aggregate root representing a research project that groups related experiments.
/// </summary>
public sealed class Project : BaseEntity
{
    /// <summary>
    /// Gets or sets the name of the project.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional description of the project.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this project is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Gets or sets the experiments belonging to this project.
    /// </summary>
    public List<Experiment> Experiments { get; set; } = [];
}

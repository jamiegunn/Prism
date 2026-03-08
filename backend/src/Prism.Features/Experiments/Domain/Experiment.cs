using Prism.Common.Database;

namespace Prism.Features.Experiments.Domain;

/// <summary>
/// Represents a single experiment within a project, containing multiple runs
/// that test hypotheses against different models or parameters.
/// </summary>
public sealed class Experiment : BaseEntity
{
    /// <summary>
    /// Gets or sets the ID of the project this experiment belongs to.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the parent project.
    /// </summary>
    public Project? Project { get; set; }

    /// <summary>
    /// Gets or sets the name of the experiment.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional description of the experiment.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status of the experiment.
    /// </summary>
    public ExperimentStatus Status { get; set; } = ExperimentStatus.Active;

    /// <summary>
    /// Gets or sets the optional hypothesis being tested.
    /// </summary>
    public string? Hypothesis { get; set; }

    /// <summary>
    /// Gets or sets the runs belonging to this experiment.
    /// </summary>
    public List<Run> Runs { get; set; } = [];
}

using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.Dtos;

/// <summary>
/// Data transfer object for an experiment.
/// </summary>
/// <param name="Id">The unique experiment identifier.</param>
/// <param name="ProjectId">The parent project ID.</param>
/// <param name="Name">The experiment name.</param>
/// <param name="Description">The optional experiment description.</param>
/// <param name="Status">The lifecycle status of the experiment.</param>
/// <param name="Hypothesis">The optional hypothesis being tested.</param>
/// <param name="RunCount">The number of runs in the experiment.</param>
/// <param name="CreatedAt">The UTC timestamp when the experiment was created.</param>
/// <param name="UpdatedAt">The UTC timestamp when the experiment was last updated.</param>
public sealed record ExperimentDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    ExperimentStatus Status,
    string? Hypothesis,
    int RunCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates an <see cref="ExperimentDto"/> from an <see cref="Experiment"/> entity.
    /// </summary>
    /// <param name="entity">The experiment entity to map.</param>
    /// <param name="runCount">The number of runs in the experiment.</param>
    /// <returns>A new <see cref="ExperimentDto"/> instance.</returns>
    public static ExperimentDto FromEntity(Experiment entity, int runCount)
    {
        return new ExperimentDto(
            entity.Id,
            entity.ProjectId,
            entity.Name,
            entity.Description,
            entity.Status,
            entity.Hypothesis,
            runCount,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}

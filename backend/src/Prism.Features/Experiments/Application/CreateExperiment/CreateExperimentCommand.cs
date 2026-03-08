namespace Prism.Features.Experiments.Application.CreateExperiment;

/// <summary>
/// Command to create a new experiment within a project.
/// </summary>
/// <param name="ProjectId">The parent project ID.</param>
/// <param name="Name">The experiment name.</param>
/// <param name="Description">The optional experiment description.</param>
/// <param name="Hypothesis">The optional hypothesis being tested.</param>
public sealed record CreateExperimentCommand(Guid ProjectId, string Name, string? Description, string? Hypothesis);

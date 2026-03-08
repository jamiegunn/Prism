using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ListExperiments;

/// <summary>
/// Query to list experiments with optional filtering by project and status.
/// </summary>
/// <param name="ProjectId">Optional project ID to filter by.</param>
/// <param name="Status">Optional status to filter by.</param>
public sealed record ListExperimentsQuery(Guid? ProjectId = null, ExperimentStatus? Status = null);

namespace Prism.Features.Experiments.Application.GetProject;

/// <summary>
/// Query to get a specific research project by ID.
/// </summary>
/// <param name="ProjectId">The project identifier.</param>
public sealed record GetProjectQuery(Guid ProjectId);

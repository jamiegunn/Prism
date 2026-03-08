namespace Prism.Features.Experiments.Application.GetRun;

/// <summary>
/// Query to get a specific run by ID.
/// </summary>
/// <param name="ExperimentId">The parent experiment ID.</param>
/// <param name="RunId">The run identifier.</param>
public sealed record GetRunQuery(Guid ExperimentId, Guid RunId);

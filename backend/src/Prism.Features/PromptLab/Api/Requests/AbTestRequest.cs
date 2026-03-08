using Prism.Features.PromptLab.Application.AbTest;

namespace Prism.Features.PromptLab.Api.Requests;

/// <summary>
/// HTTP request body for starting an A/B test.
/// </summary>
/// <param name="ProjectId">The project to create the experiment under.</param>
/// <param name="ExperimentName">The name for the created experiment.</param>
/// <param name="Variations">The prompt variations to test.</param>
/// <param name="InstanceIds">The inference instance IDs to test against.</param>
/// <param name="ParameterSets">The parameter sets to test with.</param>
/// <param name="RunsPerCombo">The number of runs per combination (default 1).</param>
public sealed record AbTestRequest(
    Guid ProjectId,
    string ExperimentName,
    List<AbTestVariation> Variations,
    List<Guid> InstanceIds,
    List<AbTestParameterSet> ParameterSets,
    int RunsPerCombo = 1);

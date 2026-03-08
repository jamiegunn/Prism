namespace Prism.Features.PromptLab.Application.AbTest;

/// <summary>
/// Command to start an A/B test across prompt variations, instances, and parameter sets.
/// </summary>
/// <param name="ProjectId">The project to create the experiment under.</param>
/// <param name="ExperimentName">The name for the created experiment.</param>
/// <param name="Variations">The prompt variations to test (version + variable values).</param>
/// <param name="InstanceIds">The inference instance IDs to test against.</param>
/// <param name="ParameterSets">The parameter sets to test with.</param>
/// <param name="RunsPerCombo">The number of runs per combination for statistical significance.</param>
public sealed record AbTestCommand(
    Guid ProjectId,
    string ExperimentName,
    List<AbTestVariation> Variations,
    List<Guid> InstanceIds,
    List<AbTestParameterSet> ParameterSets,
    int RunsPerCombo = 1);

/// <summary>
/// A single prompt variation in an A/B test.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="VersionNumber">The version number to use.</param>
/// <param name="Variables">The variable values for this variation.</param>
public sealed record AbTestVariation(
    Guid TemplateId,
    int VersionNumber,
    Dictionary<string, string> Variables);

/// <summary>
/// A parameter set for an A/B test combination.
/// </summary>
/// <param name="Temperature">The sampling temperature.</param>
/// <param name="TopP">The nucleus sampling parameter.</param>
/// <param name="MaxTokens">The maximum tokens to generate.</param>
public sealed record AbTestParameterSet(
    double? Temperature = null,
    double? TopP = null,
    int? MaxTokens = null);

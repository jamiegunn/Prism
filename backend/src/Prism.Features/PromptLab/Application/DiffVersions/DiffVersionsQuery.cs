namespace Prism.Features.PromptLab.Application.DiffVersions;

/// <summary>
/// Query to get two versions of a template for comparison.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="Version1">The first version number.</param>
/// <param name="Version2">The second version number.</param>
public sealed record DiffVersionsQuery(Guid TemplateId, int Version1, int Version2);

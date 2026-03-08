namespace Prism.Features.PromptLab.Application.GetVersion;

/// <summary>
/// Query to get a specific version of a prompt template.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="Version">The version number to retrieve.</param>
public sealed record GetVersionQuery(Guid TemplateId, int Version);

namespace Prism.Features.PromptLab.Application.ListVersions;

/// <summary>
/// Query to list all versions of a prompt template.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
public sealed record ListVersionsQuery(Guid TemplateId);

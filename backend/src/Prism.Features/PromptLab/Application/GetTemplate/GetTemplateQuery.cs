namespace Prism.Features.PromptLab.Application.GetTemplate;

/// <summary>
/// Query to get a specific prompt template by ID, including its latest version.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
public sealed record GetTemplateQuery(Guid TemplateId);

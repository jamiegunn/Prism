namespace Prism.Features.PromptLab.Application.DeleteTemplate;

/// <summary>
/// Command to delete a prompt template and all its versions.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
public sealed record DeleteTemplateCommand(Guid TemplateId);

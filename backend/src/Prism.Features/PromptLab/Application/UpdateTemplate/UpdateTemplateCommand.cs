namespace Prism.Features.PromptLab.Application.UpdateTemplate;

/// <summary>
/// Command to update a prompt template's metadata (not its version content).
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="Name">The updated template name.</param>
/// <param name="Category">The updated category.</param>
/// <param name="Description">The updated description.</param>
/// <param name="Tags">The updated tags.</param>
/// <param name="ProjectId">The updated project association.</param>
public sealed record UpdateTemplateCommand(
    Guid TemplateId,
    string Name,
    string? Category,
    string? Description,
    List<string>? Tags,
    Guid? ProjectId);

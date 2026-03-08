using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.CreateTemplate;

/// <summary>
/// Command to create a new prompt template with an initial version.
/// </summary>
/// <param name="ProjectId">The optional project to associate with.</param>
/// <param name="Name">The template name.</param>
/// <param name="Category">The optional category.</param>
/// <param name="Description">The optional description.</param>
/// <param name="Tags">The optional tags.</param>
/// <param name="SystemPrompt">The optional system prompt for the initial version.</param>
/// <param name="UserTemplate">The user template text for the initial version.</param>
/// <param name="Variables">The declared variables for the initial version.</param>
/// <param name="FewShotExamples">The few-shot examples for the initial version.</param>
public sealed record CreateTemplateCommand(
    Guid? ProjectId,
    string Name,
    string? Category,
    string? Description,
    List<string>? Tags,
    string? SystemPrompt,
    string UserTemplate,
    List<PromptVariable>? Variables,
    List<FewShotExample>? FewShotExamples);

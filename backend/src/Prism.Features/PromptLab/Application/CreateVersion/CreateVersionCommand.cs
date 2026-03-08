using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.CreateVersion;

/// <summary>
/// Command to create a new version of a prompt template.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="SystemPrompt">The optional system prompt.</param>
/// <param name="UserTemplate">The user template text.</param>
/// <param name="Variables">The declared variables.</param>
/// <param name="FewShotExamples">The few-shot examples.</param>
/// <param name="Notes">The optional release notes.</param>
public sealed record CreateVersionCommand(
    Guid TemplateId,
    string? SystemPrompt,
    string UserTemplate,
    List<PromptVariable>? Variables,
    List<FewShotExample>? FewShotExamples,
    string? Notes);

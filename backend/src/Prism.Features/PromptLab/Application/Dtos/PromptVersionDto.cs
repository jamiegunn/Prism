using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.Dtos;

/// <summary>
/// Data transfer object for a prompt template version.
/// </summary>
/// <param name="Id">The unique version identifier.</param>
/// <param name="TemplateId">The parent template ID.</param>
/// <param name="Version">The version number.</param>
/// <param name="SystemPrompt">The optional system prompt.</param>
/// <param name="UserTemplate">The user template text with variable placeholders.</param>
/// <param name="Variables">The declared variables for this version.</param>
/// <param name="FewShotExamples">The few-shot examples for this version.</param>
/// <param name="Notes">The optional release notes.</param>
/// <param name="CreatedAt">The UTC timestamp when the version was created.</param>
public sealed record PromptVersionDto(
    Guid Id,
    Guid TemplateId,
    int Version,
    string? SystemPrompt,
    string UserTemplate,
    List<PromptVariable> Variables,
    List<FewShotExample> FewShotExamples,
    string? Notes,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a <see cref="PromptVersionDto"/> from a <see cref="PromptVersion"/> entity.
    /// </summary>
    /// <param name="entity">The version entity to map.</param>
    /// <returns>A new <see cref="PromptVersionDto"/> instance.</returns>
    public static PromptVersionDto FromEntity(PromptVersion entity)
    {
        return new PromptVersionDto(
            entity.Id,
            entity.TemplateId,
            entity.Version,
            entity.SystemPrompt,
            entity.UserTemplate,
            entity.Variables,
            entity.FewShotExamples,
            entity.Notes,
            entity.CreatedAt);
    }
}

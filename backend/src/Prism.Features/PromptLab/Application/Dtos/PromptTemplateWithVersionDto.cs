namespace Prism.Features.PromptLab.Application.Dtos;

/// <summary>
/// Data transfer object for a prompt template including its latest version content.
/// </summary>
/// <param name="Template">The template metadata.</param>
/// <param name="LatestVersionContent">The content of the latest version, or null if no versions exist.</param>
public sealed record PromptTemplateWithVersionDto(
    PromptTemplateDto Template,
    PromptVersionDto? LatestVersionContent);

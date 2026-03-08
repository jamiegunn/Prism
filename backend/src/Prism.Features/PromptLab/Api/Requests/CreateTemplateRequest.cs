using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Api.Requests;

/// <summary>
/// HTTP request body for creating a new prompt template with an initial version.
/// </summary>
/// <param name="Name">The template name.</param>
/// <param name="UserTemplate">The user template text for the initial version.</param>
/// <param name="ProjectId">The optional project to associate with.</param>
/// <param name="Category">The optional category.</param>
/// <param name="Description">The optional description.</param>
/// <param name="Tags">The optional tags.</param>
/// <param name="SystemPrompt">The optional system prompt for the initial version.</param>
/// <param name="Variables">The declared variables for the initial version.</param>
/// <param name="FewShotExamples">The few-shot examples for the initial version.</param>
public sealed record CreateTemplateRequest(
    string Name,
    string UserTemplate,
    Guid? ProjectId = null,
    string? Category = null,
    string? Description = null,
    List<string>? Tags = null,
    string? SystemPrompt = null,
    List<PromptVariable>? Variables = null,
    List<FewShotExample>? FewShotExamples = null);

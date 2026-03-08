using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Api.Requests;

/// <summary>
/// HTTP request body for creating a new version of a prompt template.
/// </summary>
/// <param name="UserTemplate">The user template text.</param>
/// <param name="SystemPrompt">The optional system prompt.</param>
/// <param name="Variables">The declared variables.</param>
/// <param name="FewShotExamples">The few-shot examples.</param>
/// <param name="Notes">The optional release notes.</param>
public sealed record CreateVersionRequest(
    string UserTemplate,
    string? SystemPrompt = null,
    List<PromptVariable>? Variables = null,
    List<FewShotExample>? FewShotExamples = null,
    string? Notes = null);

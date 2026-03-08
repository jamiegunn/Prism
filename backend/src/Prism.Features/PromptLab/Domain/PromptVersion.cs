using Prism.Common.Database;

namespace Prism.Features.PromptLab.Domain;

/// <summary>
/// Represents a specific version of a prompt template's content,
/// including system prompt, user template, variables, and few-shot examples.
/// </summary>
public sealed class PromptVersion : BaseEntity
{
    /// <summary>
    /// Gets or sets the ID of the parent template.
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the parent template.
    /// </summary>
    public PromptTemplate? Template { get; set; }

    /// <summary>
    /// Gets or sets the version number (auto-incremented per template).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the optional system prompt for this version.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Gets or sets the user-facing template text containing <c>{{variable}}</c> placeholders.
    /// </summary>
    public string UserTemplate { get; set; } = "";

    /// <summary>
    /// Gets or sets the declared variables for this version.
    /// </summary>
    public List<PromptVariable> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets the few-shot examples for this version.
    /// </summary>
    public List<FewShotExample> FewShotExamples { get; set; } = [];

    /// <summary>
    /// Gets or sets optional release notes for this version.
    /// </summary>
    public string? Notes { get; set; }
}

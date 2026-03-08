using System.Text.RegularExpressions;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.Rendering;

/// <summary>
/// Renders prompt templates by substituting <c>{{variable}}</c> placeholders with provided values.
/// Validates required variables and detects undeclared variables.
/// </summary>
public sealed partial class TemplateRenderer
{
    /// <summary>
    /// Renders a prompt version into a list of chat messages ready for inference.
    /// </summary>
    /// <param name="version">The prompt version containing template text, variables, and few-shot examples.</param>
    /// <param name="variableValues">A dictionary of variable name to value mappings.</param>
    /// <returns>A result containing the rendered chat messages on success.</returns>
    public Result<RenderResult> Render(PromptVersion version, Dictionary<string, string> variableValues)
    {
        // Validate required variables are provided
        List<string> missingRequired = version.Variables
            .Where(v => v.Required && !variableValues.ContainsKey(v.Name) && v.DefaultValue is null)
            .Select(v => v.Name)
            .ToList();

        if (missingRequired.Count > 0)
        {
            return Error.Validation($"Missing required variables: {string.Join(", ", missingRequired)}");
        }

        // Build effective values: provided values + defaults
        Dictionary<string, string> effectiveValues = new(variableValues);
        foreach (PromptVariable variable in version.Variables)
        {
            if (!effectiveValues.ContainsKey(variable.Name) && variable.DefaultValue is not null)
            {
                effectiveValues[variable.Name] = variable.DefaultValue;
            }
        }

        // Detect undeclared variables in template
        HashSet<string> declaredNames = version.Variables.Select(v => v.Name).ToHashSet();
        MatchCollection templateMatches = VariablePattern().Matches(version.UserTemplate);
        List<string> undeclared = templateMatches
            .Select(m => m.Groups[1].Value)
            .Where(name => !declaredNames.Contains(name))
            .Distinct()
            .ToList();

        if (undeclared.Count > 0)
        {
            return Error.Validation($"Undeclared variables in template: {string.Join(", ", undeclared)}");
        }

        // Render the user template
        string renderedUser = VariablePattern().Replace(version.UserTemplate, match =>
        {
            string name = match.Groups[1].Value;
            return effectiveValues.TryGetValue(name, out string? value) ? value : match.Value;
        });

        // Build chat messages
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(version.SystemPrompt))
        {
            messages.Add(ChatMessage.System(version.SystemPrompt));
        }

        foreach (FewShotExample example in version.FewShotExamples)
        {
            messages.Add(ChatMessage.User(example.Input));
            messages.Add(ChatMessage.Assistant(example.Output));
        }

        messages.Add(ChatMessage.User(renderedUser));

        return new RenderResult(messages, renderedUser);
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariablePattern();
}

/// <summary>
/// The result of rendering a prompt template.
/// </summary>
/// <param name="Messages">The rendered chat messages ready for inference.</param>
/// <param name="RenderedUserPrompt">The user prompt text after variable substitution.</param>
public sealed record RenderResult(List<ChatMessage> Messages, string RenderedUserPrompt);

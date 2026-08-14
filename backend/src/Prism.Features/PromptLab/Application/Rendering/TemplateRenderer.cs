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
        List<string> undeclared = FindUndeclared(
            version.UserTemplate, version.Variables.Select(v => v.Name));

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

    /// <summary>
    /// Finds the placeholders a template uses that are not in the declared set.
    /// </summary>
    /// <param name="userTemplate">The template text.</param>
    /// <param name="declaredNames">The variable names the version declares.</param>
    /// <returns>The undeclared placeholder names, in order of first appearance, without repeats.</returns>
    /// <remarks>
    /// Shared with version creation so that both places agree on what a placeholder is. They did
    /// not have to before, and the result was a version that could be saved and never run: the
    /// check existed only at render time, so <c>{{code}}</c> without a declaration was a 201
    /// followed by a permanent validation error on every attempt to use it.
    /// </remarks>
    public static List<string> FindUndeclared(string userTemplate, IEnumerable<string> declaredNames)
    {
        if (string.IsNullOrEmpty(userTemplate))
        {
            return [];
        }

        HashSet<string> declared = [.. declaredNames];

        return [.. VariablePattern()
            .Matches(userTemplate)
            .Select(m => m.Groups[1].Value)
            .Where(name => !declared.Contains(name))
            .Distinct()];
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

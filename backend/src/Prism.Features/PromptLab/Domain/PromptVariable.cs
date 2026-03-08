namespace Prism.Features.PromptLab.Domain;

/// <summary>
/// Value object representing a variable declared in a prompt template.
/// Serialized as part of a JSON array on <see cref="PromptVersion"/>.
/// </summary>
public sealed record PromptVariable
{
    /// <summary>
    /// Gets or initializes the variable name (matches <c>{{name}}</c> in the template).
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets or initializes the variable type ("string", "number", or "boolean").
    /// </summary>
    public string Type { get; init; } = "string";

    /// <summary>
    /// Gets or initializes the optional default value for the variable.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Gets or initializes the optional description of what this variable represents.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether this variable must be provided.
    /// </summary>
    public bool Required { get; init; } = true;
}

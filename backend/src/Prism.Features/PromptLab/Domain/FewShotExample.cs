namespace Prism.Features.PromptLab.Domain;

/// <summary>
/// Value object representing a few-shot example pair for a prompt template.
/// Serialized as part of a JSON array on <see cref="PromptVersion"/>.
/// </summary>
public sealed record FewShotExample
{
    /// <summary>
    /// Gets or initializes the example user input.
    /// </summary>
    public string Input { get; init; } = "";

    /// <summary>
    /// Gets or initializes the expected assistant output.
    /// </summary>
    public string Output { get; init; } = "";

    /// <summary>
    /// Gets or initializes the optional label for this example.
    /// </summary>
    public string? Label { get; init; }
}

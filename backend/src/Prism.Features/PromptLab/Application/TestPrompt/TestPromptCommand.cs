namespace Prism.Features.PromptLab.Application.TestPrompt;

/// <summary>
/// Command to test a prompt template by rendering and executing it against an inference provider.
/// </summary>
/// <param name="TemplateId">The template identifier.</param>
/// <param name="Version">The optional version number (defaults to latest).</param>
/// <param name="Variables">The variable values to substitute into the template.</param>
/// <param name="InstanceId">The inference provider instance ID to use.</param>
/// <param name="Temperature">The optional sampling temperature.</param>
/// <param name="TopP">The optional nucleus sampling parameter.</param>
/// <param name="TopK">The optional top-K sampling parameter.</param>
/// <param name="MaxTokens">The optional maximum tokens to generate.</param>
/// <param name="Logprobs">Whether to return log probabilities.</param>
/// <param name="TopLogprobs">The number of top logprobs to return.</param>
/// <param name="SaveAsRunExperimentId">The optional experiment ID to save the result as a run.</param>
/// <param name="RunName">The optional name for the saved run.</param>
public sealed record TestPromptCommand(
    Guid TemplateId,
    int? Version,
    Dictionary<string, string> Variables,
    Guid InstanceId,
    double? Temperature,
    double? TopP,
    int? TopK,
    int? MaxTokens,
    bool Logprobs,
    int? TopLogprobs,
    Guid? SaveAsRunExperimentId,
    string? RunName);

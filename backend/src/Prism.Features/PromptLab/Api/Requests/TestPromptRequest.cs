namespace Prism.Features.PromptLab.Api.Requests;

/// <summary>
/// HTTP request body for testing a prompt template.
/// </summary>
/// <param name="Variables">The variable values to substitute into the template.</param>
/// <param name="InstanceId">The inference provider instance ID to use.</param>
/// <param name="Version">The optional version number (defaults to latest).</param>
/// <param name="Temperature">The optional sampling temperature.</param>
/// <param name="TopP">The optional nucleus sampling parameter.</param>
/// <param name="TopK">The optional top-K sampling parameter.</param>
/// <param name="MaxTokens">The optional maximum tokens to generate.</param>
/// <param name="Logprobs">Whether to return log probabilities.</param>
/// <param name="TopLogprobs">The number of top logprobs to return.</param>
/// <param name="SaveAsRunExperimentId">The optional experiment ID to save the result as a run.</param>
/// <param name="RunName">The optional name for the saved run.</param>
public sealed record TestPromptRequest(
    Dictionary<string, string> Variables,
    Guid InstanceId,
    int? Version = null,
    double? Temperature = null,
    double? TopP = null,
    int? TopK = null,
    int? MaxTokens = null,
    bool Logprobs = false,
    int? TopLogprobs = null,
    Guid? SaveAsRunExperimentId = null,
    string? RunName = null);

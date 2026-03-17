namespace Prism.Features.Experiments.Api.Requests;

/// <summary>
/// Request body for running a parameter sweep on an experiment.
/// </summary>
/// <param name="InstanceId">The inference provider instance to use.</param>
/// <param name="Input">The user prompt text.</param>
/// <param name="SystemPrompt">Optional system prompt.</param>
/// <param name="Model">Optional model override.</param>
/// <param name="PromptVersionId">Optional prompt version ID for linkage.</param>
/// <param name="TemperatureValues">Temperature values to sweep (e.g., [0.0, 0.5, 1.0]).</param>
/// <param name="TopPValues">Top-P values to sweep.</param>
/// <param name="MaxTokensValues">Max tokens values to sweep.</param>
/// <param name="CaptureLogprobs">Whether to capture logprobs data.</param>
public sealed record RunSweepRequest(
    Guid InstanceId,
    string Input,
    string? SystemPrompt = null,
    string? Model = null,
    Guid? PromptVersionId = null,
    List<double>? TemperatureValues = null,
    List<double>? TopPValues = null,
    List<int>? MaxTokensValues = null,
    bool CaptureLogprobs = false);

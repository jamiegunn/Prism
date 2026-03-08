namespace Prism.Features.TokenExplorer.Api.Requests;

/// <summary>
/// HTTP request body for the step-through endpoint.
/// </summary>
/// <param name="InstanceId">The inference instance to use for prediction.</param>
/// <param name="Prompt">The current prompt text before appending the token.</param>
/// <param name="SelectedToken">The token to append (model-chosen or user-forced).</param>
/// <param name="PreviousTokens">Previously accumulated assistant tokens from earlier steps. Empty if first step.</param>
/// <param name="TopLogprobs">Optional number of top logprob alternatives to return. Defaults to 20.</param>
/// <param name="Temperature">Optional sampling temperature. Null means greedy decoding.</param>
/// <param name="EnableThinking">Whether to enable chain-of-thought reasoning. Defaults to false.</param>
public sealed record StepThroughRequest(
    Guid InstanceId,
    string Prompt,
    string SelectedToken,
    string? PreviousTokens,
    int? TopLogprobs,
    double? Temperature,
    bool? EnableThinking);

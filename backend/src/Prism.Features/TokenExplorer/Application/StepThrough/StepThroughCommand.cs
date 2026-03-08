namespace Prism.Features.TokenExplorer.Application.StepThrough;

/// <summary>
/// Command to step through token generation by appending a selected token and predicting the next one.
/// The selected token may be the model's top choice or a user-forced alternative.
/// </summary>
/// <param name="InstanceId">The inference instance to use for prediction.</param>
/// <param name="Prompt">The current prompt text before appending the token.</param>
/// <param name="SelectedToken">The token to append to the prompt (model-chosen or user-forced).</param>
/// <param name="PreviousTokens">Previously accumulated assistant tokens from earlier steps. Empty string if this is the first step.</param>
/// <param name="TopLogprobs">The number of top logprob alternatives to return (max 20 for vLLM).</param>
/// <param name="Temperature">The sampling temperature. Null means greedy decoding (temperature 0).</param>
/// <param name="EnableThinking">Whether to enable chain-of-thought reasoning. Default false for token exploration.</param>
public sealed record StepThroughCommand(
    Guid InstanceId,
    string Prompt,
    string SelectedToken,
    string PreviousTokens = "",
    int TopLogprobs = 20,
    double? Temperature = null,
    bool EnableThinking = false);

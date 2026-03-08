namespace Prism.Features.TokenExplorer.Api.Requests;

/// <summary>
/// HTTP request body for the predict next token endpoint.
/// </summary>
/// <param name="InstanceId">The inference instance to use for prediction.</param>
/// <param name="Prompt">The input prompt text to predict the next token for.</param>
/// <param name="TopLogprobs">Optional number of top logprob alternatives to return. Defaults to 20.</param>
/// <param name="Temperature">Optional sampling temperature. Null means greedy decoding.</param>
/// <param name="EnableThinking">Whether to enable chain-of-thought reasoning. Defaults to false.</param>
public sealed record PredictNextTokenRequest(
    Guid InstanceId,
    string Prompt,
    int? TopLogprobs,
    double? Temperature,
    bool? EnableThinking);

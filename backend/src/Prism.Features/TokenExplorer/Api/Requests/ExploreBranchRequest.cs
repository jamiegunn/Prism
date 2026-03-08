namespace Prism.Features.TokenExplorer.Api.Requests;

/// <summary>
/// HTTP request body for the explore branch endpoint.
/// </summary>
/// <param name="InstanceId">The inference instance to use for generation.</param>
/// <param name="Prompt">The base prompt text before the forced token.</param>
/// <param name="ForcedToken">The token to force as the start of the generation branch.</param>
/// <param name="MaxTokens">Optional maximum number of tokens to generate. Defaults to 50.</param>
/// <param name="Temperature">Optional sampling temperature. Null means greedy decoding.</param>
/// <param name="TopLogprobs">Optional number of top logprob alternatives per token. Defaults to 5.</param>
/// <param name="EnableThinking">Whether to enable chain-of-thought reasoning. Defaults to false.</param>
public sealed record ExploreBranchRequest(
    Guid InstanceId,
    string Prompt,
    string ForcedToken,
    int? MaxTokens,
    double? Temperature,
    int? TopLogprobs,
    bool? EnableThinking);

namespace Prism.Features.TokenExplorer.Application.ExploreBranch;

/// <summary>
/// Command to explore an alternative generation branch by forcing a starting token
/// and letting the model continue for multiple tokens.
/// </summary>
/// <param name="InstanceId">The inference instance to use for generation.</param>
/// <param name="Prompt">The base prompt text before the forced token.</param>
/// <param name="ForcedToken">The token to force as the start of the generation branch.</param>
/// <param name="MaxTokens">The maximum number of tokens to generate in the branch (default 50).</param>
/// <param name="Temperature">The sampling temperature. Null means greedy decoding (temperature 0).</param>
/// <param name="TopLogprobs">The number of top logprob alternatives to return per token (default 5).</param>
/// <param name="EnableThinking">Whether to enable chain-of-thought reasoning. Default false for token exploration.</param>
public sealed record ExploreBranchCommand(
    Guid InstanceId,
    string Prompt,
    string ForcedToken,
    int MaxTokens = 50,
    double? Temperature = null,
    int TopLogprobs = 5,
    bool EnableThinking = false);

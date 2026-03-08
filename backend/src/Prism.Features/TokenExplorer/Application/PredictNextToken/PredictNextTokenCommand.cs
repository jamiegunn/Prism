namespace Prism.Features.TokenExplorer.Application.PredictNextToken;

/// <summary>
/// Command to predict the next token for a given prompt, returning top-K logprob predictions.
/// </summary>
/// <param name="InstanceId">The inference instance to use for prediction.</param>
/// <param name="Prompt">The input prompt text to predict the next token for.</param>
/// <param name="TopLogprobs">The number of top logprob alternatives to return (max 20 for vLLM).</param>
/// <param name="Temperature">The sampling temperature. Null means greedy decoding (temperature 0).</param>
/// <param name="EnableThinking">Whether to enable chain-of-thought reasoning. Default false for token exploration.</param>
/// <param name="AssistantPrefix">Optional assistant message prefix (for step-through continuation). When set, the model continues generating after this prefix.</param>
public sealed record PredictNextTokenCommand(
    Guid InstanceId,
    string Prompt,
    int TopLogprobs = 20,
    double? Temperature = null,
    bool EnableThinking = false,
    string? AssistantPrefix = null);

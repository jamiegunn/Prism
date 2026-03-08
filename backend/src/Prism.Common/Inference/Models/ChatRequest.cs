namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents a provider-agnostic chat completion request.
/// Contains all parameters needed for inference across different provider backends.
/// </summary>
public sealed record ChatRequest
{
    /// <summary>
    /// Gets or initializes the model identifier to use for inference.
    /// </summary>
    public string Model { get; init; } = "";

    /// <summary>
    /// Gets or initializes the list of messages in the conversation.
    /// </summary>
    public List<ChatMessage> Messages { get; init; } = [];

    /// <summary>
    /// Gets or initializes the sampling temperature (0.0 to 2.0).
    /// Higher values increase randomness, lower values make output more deterministic.
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Gets or initializes the nucleus sampling parameter.
    /// Only tokens with cumulative probability up to this value are considered.
    /// </summary>
    public double? TopP { get; init; }

    /// <summary>
    /// Gets or initializes the top-K sampling parameter.
    /// Only the top K most likely tokens are considered for each step.
    /// </summary>
    public int? TopK { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of tokens to generate.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Gets or initializes the list of stop sequences that halt generation.
    /// </summary>
    public List<string>? StopSequences { get; init; }

    /// <summary>
    /// Gets or initializes the frequency penalty (-2.0 to 2.0).
    /// Positive values penalize tokens based on their frequency in the text so far.
    /// </summary>
    public double? FrequencyPenalty { get; init; }

    /// <summary>
    /// Gets or initializes the presence penalty (-2.0 to 2.0).
    /// Positive values penalize tokens that have already appeared in the text.
    /// </summary>
    public double? PresencePenalty { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether to return log probabilities for output tokens.
    /// </summary>
    public bool Logprobs { get; init; }

    /// <summary>
    /// Gets or initializes the number of most likely tokens to return log probabilities for.
    /// Requires <see cref="Logprobs"/> to be true.
    /// </summary>
    public int? TopLogprobs { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether to stream the response as SSE events.
    /// </summary>
    public bool Stream { get; init; }

    /// <summary>
    /// Gets or initializes the response format for guided decoding (e.g., JSON schema).
    /// </summary>
    public string? ResponseFormat { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether to enable thinking/reasoning mode.
    /// When false, models that support chain-of-thought (e.g., Qwen3) will skip the internal
    /// reasoning step and produce direct output. Default is true.
    /// </summary>
    public bool EnableThinking { get; init; } = true;

    /// <summary>
    /// Gets or initializes the source module that originated this inference request.
    /// Used for tracking and recording purposes (e.g., "playground", "experiments", "evaluation").
    /// </summary>
    public string? SourceModule { get; init; }
}

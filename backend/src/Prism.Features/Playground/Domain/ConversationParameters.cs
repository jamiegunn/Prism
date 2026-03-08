namespace Prism.Features.Playground.Domain;

/// <summary>
/// Stores inference parameters for a conversation. Serialized as a JSON column on the conversation entity.
/// </summary>
public sealed record ConversationParameters
{
    /// <summary>
    /// Gets or initializes the sampling temperature (0.0 to 2.0).
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Gets or initializes the nucleus sampling parameter.
    /// </summary>
    public double? TopP { get; init; }

    /// <summary>
    /// Gets or initializes the top-K sampling parameter.
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
    /// </summary>
    public double? FrequencyPenalty { get; init; }

    /// <summary>
    /// Gets or initializes the presence penalty (-2.0 to 2.0).
    /// </summary>
    public double? PresencePenalty { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether to return log probabilities.
    /// </summary>
    public bool Logprobs { get; init; }

    /// <summary>
    /// Gets or initializes the number of most likely tokens to return log probabilities for.
    /// </summary>
    public int? TopLogprobs { get; init; }
}

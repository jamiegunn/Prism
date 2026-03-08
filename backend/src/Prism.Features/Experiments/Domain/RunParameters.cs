namespace Prism.Features.Experiments.Domain;

/// <summary>
/// Value object storing inference parameters for an experiment run.
/// Serialized as a JSON column on the run entity.
/// </summary>
public sealed record RunParameters
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
}

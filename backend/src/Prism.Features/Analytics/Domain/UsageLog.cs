namespace Prism.Features.Analytics.Domain;

/// <summary>
/// Records a single inference usage event for analytics tracking.
/// </summary>
public sealed class UsageLog : BaseEntity
{
    /// <summary>
    /// Gets or sets the model identifier used for this inference.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// Gets or sets the number of prompt tokens consumed.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of completion tokens generated.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Gets or sets the total latency in milliseconds.
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the time to first token in milliseconds.
    /// </summary>
    public int? TtftMs { get; set; }

    /// <summary>
    /// Gets or sets the tokens per second generation rate.
    /// </summary>
    public double? TokensPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the source module that originated this inference (e.g., "playground", "evaluation").
    /// </summary>
    public string SourceModule { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional project ID this usage is associated with.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the estimated cost in USD.
    /// </summary>
    public decimal? Cost { get; set; }
}

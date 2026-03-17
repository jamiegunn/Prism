using Prism.Common.Database;

namespace Prism.Features.History.Domain;

/// <summary>
/// A detailed technical event stream for an inference run, containing token-level data.
/// Separates deep debugging data from the user-readable <see cref="InferenceRecord"/>.
/// </summary>
public sealed class InferenceTrace : BaseEntity
{
    /// <summary>
    /// Gets or sets the ID of the inference record this trace belongs to.
    /// </summary>
    public Guid InferenceRecordId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the parent inference record.
    /// </summary>
    public InferenceRecord? InferenceRecord { get; set; }

    /// <summary>
    /// Gets or sets the total number of token events in this trace.
    /// </summary>
    public int TokenEventCount { get; set; }

    /// <summary>
    /// Gets or sets the computed perplexity for the trace.
    /// </summary>
    public double? Perplexity { get; set; }

    /// <summary>
    /// Gets or sets the mean Shannon entropy across all token positions.
    /// </summary>
    public double? MeanEntropy { get; set; }

    /// <summary>
    /// Gets or sets the average logprob across all tokens.
    /// </summary>
    public double? AverageLogprob { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens flagged as surprising (low probability).
    /// </summary>
    public int SurpriseTokenCount { get; set; }

    /// <summary>
    /// Gets or sets the surprise threshold used for flagging tokens.
    /// </summary>
    public double SurpriseThreshold { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets the schema version of this trace for backward compatibility.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the token events in this trace.
    /// </summary>
    public List<TokenEvent> TokenEvents { get; set; } = [];
}

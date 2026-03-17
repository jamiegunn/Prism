using Prism.Common.Database;

namespace Prism.Features.History.Domain;

/// <summary>
/// Represents a single token-level event within an <see cref="InferenceTrace"/>.
/// Captures the generated token, its probability, alternatives, and computed entropy.
/// </summary>
public sealed class TokenEvent : BaseEntity
{
    /// <summary>
    /// Gets or sets the ID of the trace this token event belongs to.
    /// </summary>
    public Guid InferenceTraceId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the parent trace.
    /// </summary>
    public InferenceTrace? InferenceTrace { get; set; }

    /// <summary>
    /// Gets or sets the zero-based position index of this token in the generated sequence.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the generated token text.
    /// </summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// Gets or sets the log probability of this token.
    /// </summary>
    public double Logprob { get; set; }

    /// <summary>
    /// Gets or sets the probability of this token (exp of logprob).
    /// </summary>
    public double Probability { get; set; }

    /// <summary>
    /// Gets or sets the Shannon entropy at this token position.
    /// </summary>
    public double Entropy { get; set; }

    /// <summary>
    /// Gets or sets whether this token was flagged as surprising (below threshold).
    /// </summary>
    public bool IsSurprise { get; set; }

    /// <summary>
    /// Gets or sets the byte offset of this token in the output, if available.
    /// </summary>
    public int? ByteOffset { get; set; }

    /// <summary>
    /// Gets or sets the top-K alternative tokens as serialized JSON.
    /// Stored as JSONB array of {token, logprob, probability} objects.
    /// </summary>
    public string? TopAlternativesJson { get; set; }
}

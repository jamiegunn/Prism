using Prism.Common.Inference.Models;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// The complete result of an inference execution through the runtime.
/// Contains the response, recording metadata, and computed token analysis.
/// </summary>
public sealed record InferenceRunResult
{
    /// <summary>
    /// Gets the chat response from the provider.
    /// </summary>
    public required ChatResponse Response { get; init; }

    /// <summary>
    /// Gets the unique identifier assigned to this recorded run.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the computed token analysis metrics (perplexity, entropy, surprise tokens).
    /// Contains <see cref="TokenAnalysis.Empty"/> when logprobs were not requested or unavailable.
    /// </summary>
    public required TokenAnalysis Analysis { get; init; }

    /// <summary>
    /// Gets the provider name that served this request.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets the model identifier used for this run.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Gets the environment snapshot captured at execution time.
    /// </summary>
    public EnvironmentSnapshot? Environment { get; init; }

    /// <summary>
    /// Gets the estimated cost of this run, if pricing data is available.
    /// </summary>
    public decimal? EstimatedCost { get; init; }
}

/// <summary>
/// The result of a streaming inference execution through the runtime.
/// Provides the stream plus a completion task for post-stream metadata.
/// </summary>
public sealed record StreamingInferenceRunResult
{
    /// <summary>
    /// Gets the unique identifier assigned to this recorded run.
    /// Available immediately before streaming begins.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the async enumerable of stream chunks.
    /// </summary>
    public required IAsyncEnumerable<StreamChunk> Stream { get; init; }

    /// <summary>
    /// Gets the provider name that is serving this request.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets the model identifier used for this run.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Gets a task that completes after streaming finishes, providing the full run result.
    /// The caller should await this after consuming the stream to get analysis and recording confirmation.
    /// </summary>
    public required Task<InferenceRunResult> Completion { get; init; }
}

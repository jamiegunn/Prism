using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// The central entry point for all user-facing inference execution.
/// Resolves the provider, executes inference, records the run, and computes token analysis.
/// All feature-level inference should route through this interface rather than calling
/// <see cref="IInferenceProvider"/> directly.
/// </summary>
public interface IInferenceRuntime
{
    /// <summary>
    /// Executes a non-streaming chat completion.
    /// Resolves the provider, sends the request, records the run, and computes analysis.
    /// </summary>
    /// <param name="instanceId">The provider instance identifier to use.</param>
    /// <param name="request">The chat request to execute.</param>
    /// <param name="options">Runtime options for recording, tagging, and analysis.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the full run result with response, analysis, and recording metadata.</returns>
    Task<Result<InferenceRunResult>> ExecuteAsync(
        Guid instanceId,
        ChatRequest request,
        InferenceRunOptions? options,
        CancellationToken ct);

    /// <summary>
    /// Executes a streaming chat completion.
    /// Returns immediately with the stream and a completion task for post-stream metadata.
    /// Recording and analysis happen after the stream completes.
    /// </summary>
    /// <param name="instanceId">The provider instance identifier to use.</param>
    /// <param name="request">The chat request to execute. <see cref="ChatRequest.Stream"/> is set to true automatically.</param>
    /// <param name="options">Runtime options for recording, tagging, and analysis.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the streaming run result with the chunk stream and a completion task.</returns>
    Task<Result<StreamingInferenceRunResult>> ExecuteStreamingAsync(
        Guid instanceId,
        ChatRequest request,
        InferenceRunOptions? options,
        CancellationToken ct);
}

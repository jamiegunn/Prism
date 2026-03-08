using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference;

/// <summary>
/// Core abstraction for inference providers. All inference operations in the application
/// go through this interface. Implementations exist for vLLM, Ollama, LM Studio, and
/// generic OpenAI-compatible servers.
/// </summary>
public interface IInferenceProvider
{
    /// <summary>
    /// Gets the display name of this provider instance.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the base endpoint URL of this provider.
    /// </summary>
    string Endpoint { get; }

    /// <summary>
    /// Gets the capabilities of this provider.
    /// </summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Sends a chat completion request and returns the full response.
    /// </summary>
    /// <param name="request">The chat completion request parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the chat response on success.</returns>
    Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct);

    /// <summary>
    /// Sends a streaming chat completion request and yields response chunks as they arrive.
    /// </summary>
    /// <param name="request">The chat completion request parameters. The Stream property is automatically set to true.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An async enumerable of stream chunks, each typically containing a single token.</returns>
    IAsyncEnumerable<StreamChunk> StreamChatAsync(ChatRequest request, CancellationToken ct);

    /// <summary>
    /// Retrieves information about the currently loaded model.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing model information on success.</returns>
    Task<Result<ModelInfo>> GetModelInfoAsync(CancellationToken ct);

    /// <summary>
    /// Checks the health and availability of the provider.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the health status.</returns>
    Task<Result<HealthStatus>> CheckHealthAsync(CancellationToken ct);

    /// <summary>
    /// Retrieves runtime performance metrics from the provider.
    /// Only supported by providers that expose metrics (e.g., vLLM).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing provider metrics on success.</returns>
    Task<Result<ProviderMetrics>> GetMetricsAsync(CancellationToken ct);

    /// <summary>
    /// Tokenizes a text string using the provider's tokenizer.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the tokenization response on success.</returns>
    Task<Result<TokenizeResponse>> TokenizeAsync(string text, CancellationToken ct);

    /// <summary>
    /// Detokenizes a list of token IDs back to text using the provider's tokenizer.
    /// </summary>
    /// <param name="tokenIds">The token IDs to decode.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the decoded text on success.</returns>
    Task<Result<DetokenizeResponse>> DetokenizeAsync(IReadOnlyList<int> tokenIds, CancellationToken ct);

    /// <summary>
    /// Retrieves information about the model's tokenizer including vocabulary size and special tokens.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing tokenizer information on success.</returns>
    Task<Result<TokenizerInfo>> GetTokenizerInfoAsync(CancellationToken ct);
}

using System.Runtime.CompilerServices;
using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference;

/// <summary>
/// Decorator that wraps an <see cref="IInferenceProvider"/> with SemaphoreSlim-based
/// concurrency control. Limits the number of simultaneous inference requests to prevent
/// overloading the provider.
/// </summary>
public sealed class RateLimitedInferenceProvider : IInferenceProvider, IDisposable
{
    private readonly IInferenceProvider _inner;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<RateLimitedInferenceProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitedInferenceProvider"/> class.
    /// </summary>
    /// <param name="inner">The inner provider to wrap with rate limiting.</param>
    /// <param name="maxConcurrency">The maximum number of concurrent requests allowed.</param>
    /// <param name="logger">The logger instance.</param>
    public RateLimitedInferenceProvider(
        IInferenceProvider inner,
        int maxConcurrency,
        ILogger<RateLimitedInferenceProvider> logger)
    {
        _inner = inner;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _logger = logger;
    }

    /// <summary>
    /// Gets the display name of the inner provider.
    /// </summary>
    public string ProviderName => _inner.ProviderName;

    /// <summary>
    /// Gets the endpoint URL of the inner provider.
    /// </summary>
    public string Endpoint => _inner.Endpoint;

    /// <summary>
    /// Gets the capabilities of the inner provider.
    /// </summary>
    public ProviderCapabilities Capabilities => _inner.Capabilities;

    /// <summary>
    /// Sends a rate-limited chat completion request.
    /// Waits for a semaphore slot before forwarding to the inner provider.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the chat response.</returns>
    public async Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            _logger.LogDebug("Acquired rate limit slot for {ProviderName} ({Available}/{Total} available)",
                ProviderName, _semaphore.CurrentCount, _semaphore.CurrentCount + 1);
            return await _inner.ChatAsync(request, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Sends a rate-limited streaming chat completion request.
    /// Holds the semaphore slot for the duration of the stream.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An async enumerable of stream chunks.</returns>
    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await foreach (StreamChunk chunk in _inner.StreamChatAsync(request, ct))
            {
                yield return chunk;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Retrieves model information from the inner provider (not rate-limited).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing model information.</returns>
    public Task<Result<ModelInfo>> GetModelInfoAsync(CancellationToken ct) =>
        _inner.GetModelInfoAsync(ct);

    /// <summary>
    /// Checks health of the inner provider (not rate-limited).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing health status.</returns>
    public Task<Result<HealthStatus>> CheckHealthAsync(CancellationToken ct) =>
        _inner.CheckHealthAsync(ct);

    /// <summary>
    /// Retrieves metrics from the inner provider (not rate-limited).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing provider metrics.</returns>
    public Task<Result<ProviderMetrics>> GetMetricsAsync(CancellationToken ct) =>
        _inner.GetMetricsAsync(ct);

    /// <summary>
    /// Tokenizes text using the inner provider (not rate-limited).
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the tokenization response.</returns>
    public Task<Result<TokenizeResponse>> TokenizeAsync(string text, CancellationToken ct) =>
        _inner.TokenizeAsync(text, ct);

    /// <inheritdoc />
    public Task<Result<DetokenizeResponse>> DetokenizeAsync(IReadOnlyList<int> tokenIds, CancellationToken ct) =>
        _inner.DetokenizeAsync(tokenIds, ct);

    /// <inheritdoc />
    public Task<Result<TokenizerInfo>> GetTokenizerInfoAsync(CancellationToken ct) =>
        _inner.GetTokenizerInfoAsync(ct);

    /// <summary>
    /// Disposes the semaphore used for rate limiting.
    /// </summary>
    public void Dispose()
    {
        _semaphore.Dispose();
    }
}

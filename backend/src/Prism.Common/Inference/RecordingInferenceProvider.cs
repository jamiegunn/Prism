using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference;

/// <summary>
/// Decorator that records every inference call to a <see cref="Channel{T}"/> for asynchronous
/// persistence. Captures request, response, timing data, and environment snapshot for each call.
/// </summary>
public sealed class RecordingInferenceProvider : IInferenceProvider
{
    private readonly IInferenceProvider _inner;
    private readonly Channel<InferenceRecordData> _recordChannel;
    private readonly InferenceProviderType _providerType;
    private readonly ILogger<RecordingInferenceProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingInferenceProvider"/> class.
    /// </summary>
    /// <param name="inner">The inner provider to wrap with recording.</param>
    /// <param name="recordChannel">The channel to publish inference records to.</param>
    /// <param name="providerType">The type of the inner provider.</param>
    /// <param name="logger">The logger instance.</param>
    public RecordingInferenceProvider(
        IInferenceProvider inner,
        Channel<InferenceRecordData> recordChannel,
        InferenceProviderType providerType,
        ILogger<RecordingInferenceProvider> logger)
    {
        _inner = inner;
        _recordChannel = recordChannel;
        _providerType = providerType;
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
    /// Sends a chat completion request and records the request/response pair.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the chat response.</returns>
    public async Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        DateTime startedAt = DateTime.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();

        Result<ChatResponse> result = await _inner.ChatAsync(request, ct);

        stopwatch.Stop();
        DateTime completedAt = DateTime.UtcNow;

        InferenceRecordData record = new(
            Id: Guid.NewGuid(),
            Request: request,
            Response: result.IsSuccess ? result.Value : null,
            ProviderName: ProviderName,
            ProviderType: _providerType,
            Endpoint: Endpoint,
            SourceModule: request.SourceModule,
            LatencyMs: stopwatch.ElapsedMilliseconds,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            IsSuccess: result.IsSuccess,
            ErrorMessage: result.IsFailure ? result.Error.Message : null,
            Environment: new EnvironmentSnapshot(
                ProviderType: _providerType,
                ProviderVersion: null,
                Model: request.Model,
                GpuInfo: null,
                Quantization: null,
                CapturedAt: startedAt));

        if (!_recordChannel.Writer.TryWrite(record))
        {
            _logger.LogWarning("Failed to write inference record to channel, channel may be full");
        }

        return result;
    }

    /// <summary>
    /// Sends a streaming chat completion request and records the aggregated response.
    /// Collects all chunks and records the complete interaction after the stream ends.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An async enumerable of stream chunks.</returns>
    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        DateTime startedAt = DateTime.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<StreamChunk> chunks = new();

        await foreach (StreamChunk chunk in _inner.StreamChatAsync(request, ct))
        {
            chunks.Add(chunk);
            yield return chunk;
        }

        stopwatch.Stop();
        DateTime completedAt = DateTime.UtcNow;

        StreamChunk? lastChunk = chunks.Count > 0 ? chunks[^1] : null;
        string aggregatedContent = string.Concat(chunks.Select(c => c.Content));
        UsageInfo? usage = lastChunk?.Usage;
        string? finishReason = lastChunk?.FinishReason;

        ChatResponse aggregatedResponse = new()
        {
            Content = aggregatedContent,
            FinishReason = finishReason,
            Usage = usage,
            ModelId = request.Model,
            Timing = new TimingInfo(
                LatencyMs: stopwatch.ElapsedMilliseconds,
                TtftMs: null,
                TokensPerSecond: usage is not null && stopwatch.ElapsedMilliseconds > 0
                    ? usage.CompletionTokens / (stopwatch.ElapsedMilliseconds / 1000.0)
                    : null)
        };

        InferenceRecordData record = new(
            Id: Guid.NewGuid(),
            Request: request,
            Response: aggregatedResponse,
            ProviderName: ProviderName,
            ProviderType: _providerType,
            Endpoint: Endpoint,
            SourceModule: request.SourceModule,
            LatencyMs: stopwatch.ElapsedMilliseconds,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            IsSuccess: true,
            ErrorMessage: null,
            Environment: new EnvironmentSnapshot(
                ProviderType: _providerType,
                ProviderVersion: null,
                Model: request.Model,
                GpuInfo: null,
                Quantization: null,
                CapturedAt: startedAt));

        if (!_recordChannel.Writer.TryWrite(record))
        {
            _logger.LogWarning("Failed to write streaming inference record to channel");
        }
    }

    /// <summary>
    /// Retrieves model information (pass-through, not recorded).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing model information.</returns>
    public Task<Result<ModelInfo>> GetModelInfoAsync(CancellationToken ct) =>
        _inner.GetModelInfoAsync(ct);

    /// <summary>
    /// Checks provider health (pass-through, not recorded).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing health status.</returns>
    public Task<Result<HealthStatus>> CheckHealthAsync(CancellationToken ct) =>
        _inner.CheckHealthAsync(ct);

    /// <summary>
    /// Retrieves provider metrics (pass-through, not recorded).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing provider metrics.</returns>
    public Task<Result<ProviderMetrics>> GetMetricsAsync(CancellationToken ct) =>
        _inner.GetMetricsAsync(ct);

    /// <summary>
    /// Tokenizes text (pass-through, not recorded).
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
}

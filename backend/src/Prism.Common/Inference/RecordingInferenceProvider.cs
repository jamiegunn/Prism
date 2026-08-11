using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Common.Telemetry;

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

        // Span per call, named per the GenAI conventions ("chat {model}"). This decorator
        // wraps the single point where providers are constructed, so every module's calls
        // get a span — the same property that makes recording unbypassable.
        using Activity? activity = StartInferenceActivity(request);

        Result<ChatResponse> result = await _inner.ChatAsync(request, ct);

        stopwatch.Stop();
        DateTime completedAt = DateTime.UtcNow;

        if (activity is not null)
        {
            if (result.IsSuccess)
            {
                SetResponseAttributes(activity, result.Value);
            }
            else
            {
                activity.SetTag(GenAiAttributes.ErrorType, result.Error.Code);
                activity.SetStatus(ActivityStatusCode.Error, result.Error.Message);
            }
        }

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
                CapturedAt: startedAt),
            TraceId: activity?.TraceId.ToString(),
            SpanId: activity?.SpanId.ToString());

        if (!_recordChannel.Writer.TryWrite(record))
        {
            _logger.LogWarning("Failed to write inference record to channel, channel may be full");
        }

        return result;
    }

    /// <summary>
    /// Starts the inference span and sets the request-side <c>gen_ai.*</c> attributes.
    /// Returns null when no tracing listener is active — spans cost nothing when nobody is
    /// collecting them.
    /// </summary>
    /// <param name="request">The request being sent.</param>
    /// <returns>The started activity, or null.</returns>
    private Activity? StartInferenceActivity(ChatRequest request)
    {
        Activity? activity = PrismTelemetry.InferenceSource.StartActivity(
            $"chat {request.Model}", ActivityKind.Client);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(GenAiAttributes.OperationName, "chat");
        activity.SetTag(GenAiAttributes.System, ProviderSystemName(_providerType));
        activity.SetTag(GenAiAttributes.RequestModel, request.Model);

        if (request.Temperature is not null)
        {
            activity.SetTag(GenAiAttributes.RequestTemperature, request.Temperature.Value);
        }

        if (request.TopP is not null)
        {
            activity.SetTag(GenAiAttributes.RequestTopP, request.TopP.Value);
        }

        if (request.MaxTokens is not null)
        {
            activity.SetTag(GenAiAttributes.RequestMaxTokens, request.MaxTokens.Value);
        }

        // Content is opt-in per the convention, and off by default: prompts are sensitive,
        // and a trace pipeline usually ships somewhere logs do not.
        if (PrismTelemetry.CaptureContent)
        {
            activity.SetTag(
                GenAiAttributes.PromptContent,
                string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}")));
        }

        return activity;
    }

    /// <summary>
    /// Sets the response-side <c>gen_ai.*</c> attributes on a span.
    /// </summary>
    /// <param name="activity">The span.</param>
    /// <param name="response">The successful response.</param>
    private static void SetResponseAttributes(Activity activity, ChatResponse response)
    {
        if (!string.IsNullOrEmpty(response.ModelId))
        {
            activity.SetTag(GenAiAttributes.ResponseModel, response.ModelId);
        }

        if (response.FinishReason is not null)
        {
            activity.SetTag(
                GenAiAttributes.ResponseFinishReasons, new[] { response.FinishReason });
        }

        if (response.Usage is not null)
        {
            activity.SetTag(GenAiAttributes.UsageInputTokens, response.Usage.PromptTokens);
            activity.SetTag(GenAiAttributes.UsageOutputTokens, response.Usage.CompletionTokens);
        }

        if (PrismTelemetry.CaptureContent)
        {
            activity.SetTag(GenAiAttributes.CompletionContent, response.Content);
        }
    }

    /// <summary>
    /// Maps the provider type to the GenAI <c>gen_ai.system</c> value.
    /// </summary>
    /// <param name="type">The provider type.</param>
    /// <returns>The lowercase system name.</returns>
    internal static string ProviderSystemName(InferenceProviderType type) => type switch
    {
        InferenceProviderType.Ollama => "ollama",
        InferenceProviderType.Vllm => "vllm",
        InferenceProviderType.LmStudio => "lm_studio",
        _ => "openai_compatible",
    };

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

        // The span covers the whole stream: opened before the first token, closed after the
        // last, so its duration is the user-visible latency.
        using Activity? activity = StartInferenceActivity(request);

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

        // Streamed calls carry their logprobs one chunk at a time; dropping them here was
        // why a streamed Playground call had no perplexity, entropy or token trace in
        // History while the identical non-streamed call did.
        List<TokenLogprob> streamedLogprobs = chunks
            .Where(c => c.LogprobsEntry is not null)
            .Select(c => c.LogprobsEntry!)
            .ToList();

        ChatResponse aggregatedResponse = new()
        {
            Content = aggregatedContent,
            FinishReason = finishReason,
            Usage = usage,
            LogprobsData = streamedLogprobs.Count > 0
                ? new LogprobsData { Tokens = streamedLogprobs }
                : null,
            ModelId = request.Model,
            Timing = new TimingInfo(
                LatencyMs: stopwatch.ElapsedMilliseconds,
                TtftMs: null,
                TokensPerSecond: usage is not null && stopwatch.ElapsedMilliseconds > 0
                    ? usage.CompletionTokens / (stopwatch.ElapsedMilliseconds / 1000.0)
                    : null)
        };

        if (activity is not null)
        {
            SetResponseAttributes(activity, aggregatedResponse);
        }

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
                CapturedAt: startedAt),
            TraceId: activity?.TraceId.ToString(),
            SpanId: activity?.SpanId.ToString());

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

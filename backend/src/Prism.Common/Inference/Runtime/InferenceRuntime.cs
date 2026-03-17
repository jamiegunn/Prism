using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Prism.Common.Inference.Metrics;
using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Default implementation of <see cref="IInferenceRuntime"/>.
/// Orchestrates provider resolution, execution, recording, and analysis for every inference call.
/// </summary>
public sealed class InferenceRuntime : IInferenceRuntime
{
    private readonly IRuntimeProviderResolver _resolver;
    private readonly IInferenceRecorder _recorder;
    private readonly ITokenAnalysisService _analysisService;
    private readonly ILogger<InferenceRuntime> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InferenceRuntime"/> class.
    /// </summary>
    /// <param name="resolver">The resolver for looking up inference providers by instance ID.</param>
    /// <param name="recorder">The recorder for persisting run history.</param>
    /// <param name="analysisService">The service for computing token-level metrics.</param>
    /// <param name="logger">The logger for runtime operations.</param>
    public InferenceRuntime(
        IRuntimeProviderResolver resolver,
        IInferenceRecorder recorder,
        ITokenAnalysisService analysisService,
        ILogger<InferenceRuntime> logger)
    {
        _resolver = resolver;
        _recorder = recorder;
        _analysisService = analysisService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<InferenceRunResult>> ExecuteAsync(
        Guid instanceId,
        ChatRequest request,
        InferenceRunOptions? options,
        CancellationToken ct)
    {
        options ??= InferenceRunOptions.Default;
        Guid runId = Guid.NewGuid();

        Result<IInferenceProvider> providerResult = await _resolver.ResolveAsync(instanceId, ct);
        if (providerResult.IsFailure)
        {
            return Result<InferenceRunResult>.Failure(providerResult.Error);
        }

        IInferenceProvider provider = providerResult.Value;
        ChatRequest taggedRequest = request with { SourceModule = options.SourceModule ?? request.SourceModule };

        _logger.LogInformation(
            "Executing inference {RunId} on {ProviderName} model {Model}",
            runId, provider.ProviderName, request.Model);

        var stopwatch = Stopwatch.StartNew();
        DateTime startedAt = DateTime.UtcNow;

        Result<ChatResponse> chatResult = await provider.ChatAsync(taggedRequest, ct);
        stopwatch.Stop();

        DateTime completedAt = DateTime.UtcNow;

        if (chatResult.IsFailure)
        {
            await RecordFailure(runId, provider, taggedRequest, options, startedAt, completedAt,
                stopwatch.ElapsedMilliseconds, chatResult.Error.Message, ct);

            return Result<InferenceRunResult>.Failure(chatResult.Error);
        }

        ChatResponse response = chatResult.Value;
        TokenAnalysis analysis = _analysisService.Analyze(response.LogprobsData, options.SurpriseThreshold);

        decimal? estimatedCost = response.Usage is not null
            ? CostCalculator.EstimateCost(response.ModelId, response.Usage.PromptTokens, response.Usage.CompletionTokens)
            : null;
        if (estimatedCost == 0) estimatedCost = null;

        EnvironmentSnapshot environment = new(
            provider.Capabilities.SupportsMetrics ? InferenceProviderType.Vllm : InferenceProviderType.OpenAiCompatible,
            null,
            response.ModelId,
            null,
            null,
            DateTime.UtcNow);

        InferenceRecordData recordData = new(
            runId,
            taggedRequest,
            response,
            provider.ProviderName,
            ResolveProviderType(provider),
            provider.Endpoint,
            options.SourceModule,
            stopwatch.ElapsedMilliseconds,
            startedAt,
            completedAt,
            true,
            null,
            environment);

        await _recorder.RecordAsync(recordData, options, ct);

        _logger.LogInformation(
            "Inference {RunId} completed in {LatencyMs}ms, {TotalTokens} tokens, perplexity {Perplexity:F2}",
            runId, stopwatch.ElapsedMilliseconds,
            response.Usage?.TotalTokens ?? 0,
            analysis.HasData ? analysis.Perplexity : 0);

        return Result<InferenceRunResult>.Success(new InferenceRunResult
        {
            Response = response,
            RunId = runId,
            Analysis = analysis,
            ProviderName = provider.ProviderName,
            ModelId = response.ModelId,
            Environment = environment,
            EstimatedCost = estimatedCost
        });
    }

    /// <inheritdoc />
    public async Task<Result<StreamingInferenceRunResult>> ExecuteStreamingAsync(
        Guid instanceId,
        ChatRequest request,
        InferenceRunOptions? options,
        CancellationToken ct)
    {
        options ??= InferenceRunOptions.Default;
        Guid runId = Guid.NewGuid();

        Result<IInferenceProvider> providerResult = await _resolver.ResolveAsync(instanceId, ct);
        if (providerResult.IsFailure)
        {
            return Result<StreamingInferenceRunResult>.Failure(providerResult.Error);
        }

        IInferenceProvider provider = providerResult.Value;
        ChatRequest streamRequest = request with
        {
            Stream = true,
            SourceModule = options.SourceModule ?? request.SourceModule
        };

        _logger.LogInformation(
            "Starting streaming inference {RunId} on {ProviderName} model {Model}",
            runId, provider.ProviderName, request.Model);

        var completionSource = new TaskCompletionSource<InferenceRunResult>();

        IAsyncEnumerable<StreamChunk> wrappedStream = WrapStream(
            runId, provider, streamRequest, options, completionSource, ct);

        return Result<StreamingInferenceRunResult>.Success(new StreamingInferenceRunResult
        {
            RunId = runId,
            Stream = wrappedStream,
            ProviderName = provider.ProviderName,
            ModelId = request.Model,
            Completion = completionSource.Task
        });
    }

    /// <summary>
    /// Wraps the provider stream to intercept chunks for recording and analysis after completion.
    /// </summary>
    private async IAsyncEnumerable<StreamChunk> WrapStream(
        Guid runId,
        IInferenceProvider provider,
        ChatRequest request,
        InferenceRunOptions options,
        TaskCompletionSource<InferenceRunResult> completionSource,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        DateTime startedAt = DateTime.UtcNow;
        var chunks = new List<StreamChunk>();
        long? ttftMs = null;

        Exception? streamException = null;

        IAsyncEnumerable<StreamChunk> stream = provider.StreamChatAsync(request, ct);

        await using IAsyncEnumerator<StreamChunk> enumerator = stream.GetAsyncEnumerator(ct);

        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception ex)
            {
                streamException = ex;
                break;
            }

            if (!hasNext)
            {
                break;
            }

            StreamChunk chunk = enumerator.Current;
            chunks.Add(chunk);

            if (chunk.IsFirst && ttftMs is null)
            {
                ttftMs = stopwatch.ElapsedMilliseconds;
            }

            yield return chunk;
        }

        stopwatch.Stop();
        DateTime completedAt = DateTime.UtcNow;

        if (streamException is not null)
        {
            await RecordFailure(runId, provider, request, options, startedAt, completedAt,
                stopwatch.ElapsedMilliseconds, streamException.Message, ct);

            completionSource.SetException(streamException);
            yield break;
        }

        // Aggregate response from chunks
        string content = string.Concat(chunks.Select(c => c.Content));
        UsageInfo? usage = chunks.LastOrDefault(c => c.Usage is not null)?.Usage;
        string? finishReason = chunks.LastOrDefault(c => c.FinishReason is not null)?.FinishReason;

        List<TokenLogprob> logprobEntries = chunks
            .Where(c => c.LogprobsEntry is not null)
            .Select(c => c.LogprobsEntry!)
            .ToList();

        LogprobsData? logprobsData = logprobEntries.Count > 0
            ? new LogprobsData { Tokens = logprobEntries }
            : null;

        double? tokensPerSecond = usage is not null && stopwatch.ElapsedMilliseconds > 0
            ? usage.CompletionTokens / (stopwatch.ElapsedMilliseconds / 1000.0)
            : null;

        ChatResponse response = new()
        {
            Content = content,
            FinishReason = finishReason,
            Usage = usage,
            LogprobsData = logprobsData,
            ModelId = request.Model,
            Timing = new TimingInfo(stopwatch.ElapsedMilliseconds, ttftMs, tokensPerSecond)
        };

        TokenAnalysis analysis = _analysisService.Analyze(logprobsData, options.SurpriseThreshold);

        decimal? estimatedCost = usage is not null
            ? CostCalculator.EstimateCost(request.Model, usage.PromptTokens, usage.CompletionTokens)
            : null;
        if (estimatedCost == 0) estimatedCost = null;

        EnvironmentSnapshot environment = new(
            ResolveProviderType(provider),
            null,
            request.Model,
            null,
            null,
            DateTime.UtcNow);

        InferenceRecordData recordData = new(
            runId,
            request,
            response,
            provider.ProviderName,
            ResolveProviderType(provider),
            provider.Endpoint,
            options.SourceModule,
            stopwatch.ElapsedMilliseconds,
            startedAt,
            completedAt,
            true,
            null,
            environment);

        await _recorder.RecordAsync(recordData, options, ct);

        _logger.LogInformation(
            "Streaming inference {RunId} completed in {LatencyMs}ms, TTFT {TtftMs}ms, {TotalTokens} tokens",
            runId, stopwatch.ElapsedMilliseconds, ttftMs, usage?.TotalTokens ?? 0);

        InferenceRunResult result = new()
        {
            Response = response,
            RunId = runId,
            Analysis = analysis,
            ProviderName = provider.ProviderName,
            ModelId = request.Model,
            Environment = environment,
            EstimatedCost = estimatedCost
        };

        completionSource.SetResult(result);
    }

    /// <summary>
    /// Records a failed inference attempt for history and debugging.
    /// </summary>
    private async Task RecordFailure(
        Guid runId,
        IInferenceProvider provider,
        ChatRequest request,
        InferenceRunOptions options,
        DateTime startedAt,
        DateTime completedAt,
        long latencyMs,
        string errorMessage,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "Inference {RunId} failed on {ProviderName}: {Error}",
            runId, provider.ProviderName, errorMessage);

        InferenceRecordData failureRecord = new(
            runId,
            request,
            null,
            provider.ProviderName,
            ResolveProviderType(provider),
            provider.Endpoint,
            options.SourceModule,
            latencyMs,
            startedAt,
            completedAt,
            false,
            errorMessage,
            null);

        await _recorder.RecordAsync(failureRecord, options, ct);
    }

    /// <summary>
    /// Resolves the provider type from the provider instance.
    /// </summary>
    private static InferenceProviderType ResolveProviderType(IInferenceProvider provider)
    {
        return provider switch
        {
            Providers.VllmProvider => InferenceProviderType.Vllm,
            Providers.OllamaProvider => InferenceProviderType.Ollama,
            _ => InferenceProviderType.OpenAiCompatible
        };
    }
}

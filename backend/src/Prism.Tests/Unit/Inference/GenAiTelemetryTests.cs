using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Common.Telemetry;

namespace Prism.Tests.Unit.Inference;

/// <summary>
/// Proofs for the gen_ai.* inference spans: an in-memory listener captures what
/// <see cref="RecordingInferenceProvider"/> emits, and every attribute name is asserted
/// against the <see cref="GenAiAttributes"/> constants — the convention, not a string
/// literal. Also proves content stays off the span until explicitly opted in, and that the
/// span's ids are the ones recorded to History.
/// </summary>
public sealed class GenAiTelemetryTests : IDisposable
{
    private readonly List<Activity> _captured = [];
    private readonly ActivityListener _listener;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenAiTelemetryTests"/> class, hooking an
    /// in-memory listener onto the Prism.Inference source.
    /// </summary>
    public GenAiTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PrismTelemetry.InferenceSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _captured.Add(activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _listener.Dispose();
        PrismTelemetry.CaptureContent = false;
    }

    private static ChatRequest Request() => new()
    {
        Model = "test-model:7b",
        Messages = [ChatMessage.User("What is 2+2?")],
        Temperature = 0.4,
        TopP = 0.9,
        MaxTokens = 64,
    };

    private static (RecordingInferenceProvider Provider, Channel<InferenceRecordData> Channel)
        BuildProvider(IInferenceProvider inner)
    {
        Channel<InferenceRecordData> channel = Channel.CreateUnbounded<InferenceRecordData>();

        var provider = new RecordingInferenceProvider(
            inner, channel, InferenceProviderType.Ollama,
            NullLogger<RecordingInferenceProvider>.Instance);

        return (provider, channel);
    }

    /// <summary>
    /// A successful call produces a client span named per the convention, carrying every
    /// request and response attribute §4.1 names — system, request model, response model,
    /// token counts, temperature, top-p, finish reasons — and the ids recorded to History
    /// are the span's own.
    /// </summary>
    [Fact]
    public async Task Successful_Call_Emits_The_GenAi_Attributes_And_Records_Its_Ids()
    {
        (RecordingInferenceProvider provider, Channel<InferenceRecordData> channel) =
            BuildProvider(new StubProvider(Result<ChatResponse>.Success(new ChatResponse
            {
                Content = "4",
                FinishReason = "stop",
                ModelId = "test-model:7b-q4",
                Usage = new UsageInfo(12, 3, 15),
            })));

        Result<ChatResponse> result = await provider.ChatAsync(Request(), CancellationToken.None);
        Assert.True(result.IsSuccess);

        Activity span = Assert.Single(_captured);

        Assert.Equal("chat test-model:7b", span.DisplayName);
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.NotEqual(ActivityStatusCode.Error, span.Status);

        Dictionary<string, object?> tags = span.TagObjects.ToDictionary(t => t.Key, t => t.Value);

        Assert.Equal("chat", tags[GenAiAttributes.OperationName]);
        Assert.Equal("ollama", tags[GenAiAttributes.System]);
        Assert.Equal("test-model:7b", tags[GenAiAttributes.RequestModel]);
        Assert.Equal(0.4, tags[GenAiAttributes.RequestTemperature]);
        Assert.Equal(0.9, tags[GenAiAttributes.RequestTopP]);
        Assert.Equal(64, tags[GenAiAttributes.RequestMaxTokens]);
        Assert.Equal("test-model:7b-q4", tags[GenAiAttributes.ResponseModel]);
        Assert.Equal(12, tags[GenAiAttributes.UsageInputTokens]);
        Assert.Equal(3, tags[GenAiAttributes.UsageOutputTokens]);
        Assert.Equal(new[] { "stop" }, (string[])tags[GenAiAttributes.ResponseFinishReasons]!);

        // The History record carries the span's ids — the correlation the UI displays.
        Assert.True(channel.Reader.TryRead(out InferenceRecordData? record));
        Assert.Equal(span.TraceId.ToString(), record!.TraceId);
        Assert.Equal(span.SpanId.ToString(), record.SpanId);
        Assert.Equal(32, record.TraceId!.Length);
        Assert.Equal(16, record.SpanId!.Length);
    }

    /// <summary>
    /// A failed call carries error status and the error.type attribute — a trace that shows
    /// failures as ordinary spans hides exactly the calls worth finding.
    /// </summary>
    [Fact]
    public async Task Failed_Call_Carries_Error_Status()
    {
        (RecordingInferenceProvider provider, Channel<InferenceRecordData> channel) =
            BuildProvider(new StubProvider(Result<ChatResponse>.Failure(
                Error.Unavailable("connection refused"))));

        Result<ChatResponse> result = await provider.ChatAsync(Request(), CancellationToken.None);
        Assert.True(result.IsFailure);

        Activity span = Assert.Single(_captured);

        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("connection refused", span.StatusDescription);

        Dictionary<string, object?> tags = span.TagObjects.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal("Unavailable", tags[GenAiAttributes.ErrorType]);

        // Failures are recorded with trace ids too — they are the rows most worth finding.
        Assert.True(channel.Reader.TryRead(out InferenceRecordData? record));
        Assert.Equal(span.TraceId.ToString(), record!.TraceId);
    }

    /// <summary>
    /// Prompt and completion content stay off the span by default — the convention makes
    /// content opt-in and it is sensitive. Only the explicit switch attaches it.
    /// </summary>
    [Fact]
    public async Task Content_Is_Opt_In_And_Off_By_Default()
    {
        (RecordingInferenceProvider provider, _) =
            BuildProvider(new StubProvider(Result<ChatResponse>.Success(new ChatResponse
            {
                Content = "secret answer",
            })));

        Assert.False(PrismTelemetry.CaptureContent, "content capture must default to off");

        await provider.ChatAsync(Request(), CancellationToken.None);

        Dictionary<string, object?> offTags =
            Assert.Single(_captured).TagObjects.ToDictionary(t => t.Key, t => t.Value);

        Assert.DoesNotContain(GenAiAttributes.PromptContent, offTags.Keys);
        Assert.DoesNotContain(GenAiAttributes.CompletionContent, offTags.Keys);

        _captured.Clear();

        try
        {
            PrismTelemetry.CaptureContent = true;

            await provider.ChatAsync(Request(), CancellationToken.None);

            Dictionary<string, object?> onTags =
                Assert.Single(_captured).TagObjects.ToDictionary(t => t.Key, t => t.Value);

            Assert.Contains("What is 2+2?", (string)onTags[GenAiAttributes.PromptContent]!);
            Assert.Equal("secret answer", onTags[GenAiAttributes.CompletionContent]);
        }
        finally
        {
            PrismTelemetry.CaptureContent = false;
        }
    }

    /// <summary>
    /// Streaming calls get a span covering the whole stream, with the aggregated usage on it
    /// and the ids recorded.
    /// </summary>
    [Fact]
    public async Task Streaming_Call_Emits_A_Span_Covering_The_Stream()
    {
        (RecordingInferenceProvider provider, Channel<InferenceRecordData> channel) =
            BuildProvider(new StubProvider(Result<ChatResponse>.Success(new ChatResponse())));

        List<StreamChunk> chunks = [];
        await foreach (StreamChunk chunk in provider.StreamChatAsync(Request(), CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(2, chunks.Count);

        Activity span = Assert.Single(_captured);
        Assert.Equal("chat test-model:7b", span.DisplayName);

        Dictionary<string, object?> tags = span.TagObjects.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal(5, tags[GenAiAttributes.UsageInputTokens]);
        Assert.Equal(2, tags[GenAiAttributes.UsageOutputTokens]);

        Assert.True(channel.Reader.TryRead(out InferenceRecordData? record));
        Assert.Equal(span.TraceId.ToString(), record!.TraceId);
    }

    /// <summary>
    /// With no tracing listener registered (OpenTelemetry off), calls still work and the
    /// recorded trace ids are null — absent, not empty strings or zeros.
    /// </summary>
    [Fact]
    public async Task Without_A_Listener_Trace_Ids_Are_Null()
    {
        _listener.Dispose(); // simulate: no tracing configured

        (RecordingInferenceProvider provider, Channel<InferenceRecordData> channel) =
            BuildProvider(new StubProvider(Result<ChatResponse>.Success(new ChatResponse())));

        Result<ChatResponse> result = await provider.ChatAsync(Request(), CancellationToken.None);
        Assert.True(result.IsSuccess);

        Assert.True(channel.Reader.TryRead(out InferenceRecordData? record));
        Assert.Null(record!.TraceId);
        Assert.Null(record.SpanId);
    }

    private sealed class StubProvider : IInferenceProvider
    {
        private readonly Result<ChatResponse> _result;

        public StubProvider(Result<ChatResponse> result) => _result = result;

        public string ProviderName => "stub";

        public string Endpoint => "http://localhost:0";

        public ProviderCapabilities Capabilities => new();

        public Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct)
            => Task.FromResult(_result);

        public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
            ChatRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield return new StreamChunk { Content = "4" };
            yield return new StreamChunk
            {
                Content = "!",
                FinishReason = "stop",
                Usage = new UsageInfo(5, 2, 7),
            };
        }

        public Task<Result<ModelInfo>> GetModelInfoAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Result<HealthStatus>> CheckHealthAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Result<ProviderMetrics>> GetMetricsAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Result<TokenizeResponse>> TokenizeAsync(string text, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Result<DetokenizeResponse>> DetokenizeAsync(IReadOnlyList<int> tokenIds, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Result<TokenizerInfo>> GetTokenizerInfoAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }
}

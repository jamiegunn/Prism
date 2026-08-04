using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Analytics.Domain;
using Prism.Features.History.Domain;
using Prism.Features.History.Infrastructure;
using Prism.Features.Models.Application;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers the claim that every inference call is recorded.
/// </summary>
/// <remarks>
/// The audit found that claim false: only the replay service routed through the recording
/// path, while Playground, Token Explorer, RAG, Agents, Experiments and Prompt Lab reached
/// providers directly. Consequently <c>UsageLog</c> had no writer at all and Analytics
/// aggregated a permanently empty table, while the History page looked populated only because
/// a seeder inserted hand-written rows.
/// </remarks>
[Collection("Database")]
public sealed class RecordingSpineTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingSpineTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public RecordingSpineTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Recording is applied by the factory, so a provider obtained the only way features can
    /// obtain one is always recorded. This is the property that makes bypassing impossible.
    /// </summary>
    [Fact]
    public async Task Every_Provider_The_Factory_Creates_Records_Its_Calls()
    {
        Channel<InferenceRecordData> channel = Channel.CreateUnbounded<InferenceRecordData>();

        var factory = new InferenceProviderFactory(
            SuccessfulChatTransport(),
            NullLoggerFactory.Instance,
            options: null,
            recordChannel: channel);

        IInferenceProvider provider = factory.CreateProvider(
            "test", "http://localhost:9999", InferenceProviderType.OpenAiCompatible);

        Result<ChatResponse> response = await provider.ChatAsync(
            new ChatRequest { Model = "gpt-4", Messages = [ChatMessage.User("hi")] },
            CancellationToken.None);

        Assert.True(response.IsSuccess);

        bool recorded = channel.Reader.TryRead(out InferenceRecordData? record);
        Assert.True(recorded, "The call completed without being recorded.");
        Assert.Equal("gpt-4", record!.Request.Model);
    }

    /// <summary>
    /// The Analytics projection must carry real token counts and latency through from the call.
    /// </summary>
    [Fact]
    public void UsageLog_Projection_Carries_Tokens_And_Latency()
    {
        UsageLog log = InferenceRecordPersistenceService.BuildUsageLog(
            RecordData("gpt-4", promptTokens: 10, completionTokens: 20, latencyMs: 123),
            new InferenceRecord { TokensPerSecond = 42.0 });

        Assert.Equal("gpt-4", log.Model);
        Assert.Equal(10, log.PromptTokens);
        Assert.Equal(20, log.CompletionTokens);
        Assert.Equal(123, log.LatencyMs);
        Assert.Equal(42.0, log.TokensPerSecond);
        Assert.Equal("playground", log.SourceModule);
    }

    /// <summary>
    /// A priced model must produce a cost.
    /// </summary>
    [Fact]
    public void UsageLog_Projection_Prices_Known_Models()
    {
        UsageLog log = InferenceRecordPersistenceService.BuildUsageLog(
            RecordData("gpt-4", promptTokens: 1000, completionTokens: 1000, latencyMs: 1),
            new InferenceRecord());

        Assert.NotNull(log.Cost);
        Assert.True(log.Cost > 0m, "A priced model produced a zero cost.");
    }

    /// <summary>
    /// A local model has no price. That must read as "not priced" rather than "free" — the
    /// distinction is the difference between an unknown cost and a claim of zero cost.
    /// </summary>
    [Fact]
    public void UsageLog_Projection_Leaves_Unpriced_Models_Null_Not_Zero()
    {
        UsageLog log = InferenceRecordPersistenceService.BuildUsageLog(
            RecordData("meta-llama/Llama-3.1-8B-Instruct", 1000, 1000, latencyMs: 1),
            new InferenceRecord());

        Assert.Null(log.Cost);
    }

    private static InferenceRecordData RecordData(
        string model, int promptTokens, int completionTokens, long latencyMs)
        => new(
            Guid.NewGuid(),
            new ChatRequest { Model = model, Messages = [ChatMessage.User("hi")] },
            new ChatResponse
            {
                Content = "hello",
                Usage = new UsageInfo(promptTokens, completionTokens, promptTokens + completionTokens),
            },
            "test-provider",
            InferenceProviderType.OpenAiCompatible,
            "http://localhost:9999",
            "playground",
            latencyMs,
            DateTime.UtcNow,
            DateTime.UtcNow,
            IsSuccess: true,
            ErrorMessage: null,
            Environment: null);

    private static FakeHttpTransport SuccessfulChatTransport() =>
        FakeHttpTransport.Json(
            """
            {
              "id": "chatcmpl-1",
              "object": "chat.completion",
              "model": "gpt-4",
              "choices": [
                {"index": 0, "message": {"role": "assistant", "content": "hello"}, "finish_reason": "stop"}
              ],
              "usage": {"prompt_tokens": 10, "completion_tokens": 20, "total_tokens": 30}
            }
            """);
}

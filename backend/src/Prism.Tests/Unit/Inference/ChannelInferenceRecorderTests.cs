using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Inference.Runtime;
using Prism.Common.Results;

namespace Prism.Tests.Unit.Inference;

public sealed class ChannelInferenceRecorderTests
{
    private readonly ILogger<ChannelInferenceRecorder> _logger = Substitute.For<ILogger<ChannelInferenceRecorder>>();

    [Fact]
    public async Task RecordAsync_WritesToChannel_WhenNotSkipped()
    {
        Channel<InferenceRecordData> channel = Channel.CreateUnbounded<InferenceRecordData>();
        var sut = new ChannelInferenceRecorder(channel, _logger);
        InferenceRecordData record = CreateTestRecord();
        var options = new InferenceRunOptions { SkipRecording = false };

        Result result = await sut.RecordAsync(record, options, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        bool hasItem = channel.Reader.TryRead(out InferenceRecordData? written);
        hasItem.Should().BeTrue();
        written!.Id.Should().Be(record.Id);
    }

    [Fact]
    public async Task RecordAsync_SkipsWrite_WhenSkipRecordingTrue()
    {
        Channel<InferenceRecordData> channel = Channel.CreateUnbounded<InferenceRecordData>();
        var sut = new ChannelInferenceRecorder(channel, _logger);
        InferenceRecordData record = CreateTestRecord();
        var options = new InferenceRunOptions { SkipRecording = true };

        Result result = await sut.RecordAsync(record, options, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        channel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task RecordAsync_ReturnsFailure_WhenChannelFull()
    {
        Channel<InferenceRecordData> channel = Channel.CreateBounded<InferenceRecordData>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

        var sut = new ChannelInferenceRecorder(channel, _logger);
        var options = new InferenceRunOptions { SkipRecording = false };

        // Fill the channel
        InferenceRecordData first = CreateTestRecord();
        await sut.RecordAsync(first, options, CancellationToken.None);

        // The second write should fail since DropWrite mode means TryWrite returns false when full
        // Actually DropWrite drops the oldest and still returns true. Use Wait mode instead.
        // Let's recreate with a channel that will reject writes.
        Channel<InferenceRecordData> fullChannel = Channel.CreateBounded<InferenceRecordData>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.Wait });
        var sut2 = new ChannelInferenceRecorder(fullChannel, _logger);

        // Fill it
        InferenceRecordData filler = CreateTestRecord();
        await sut2.RecordAsync(filler, options, CancellationToken.None);

        // Now the channel is full and TryWrite should return false
        InferenceRecordData overflow = CreateTestRecord();
        Result result = await sut2.RecordAsync(overflow, options, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("channel is full");
    }

    private static InferenceRecordData CreateTestRecord()
    {
        return new InferenceRecordData(
            Id: Guid.NewGuid(),
            Request: new ChatRequest { Model = "test-model" },
            Response: new ChatResponse { Content = "test", ModelId = "test-model" },
            ProviderName: "test-provider",
            ProviderType: InferenceProviderType.OpenAiCompatible,
            Endpoint: "http://localhost:8000",
            SourceModule: "test",
            LatencyMs: 100,
            StartedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow,
            IsSuccess: true,
            ErrorMessage: null,
            Environment: null);
    }
}

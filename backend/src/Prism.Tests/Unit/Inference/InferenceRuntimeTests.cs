using Microsoft.Extensions.Logging;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Inference.Runtime;
using Prism.Common.Results;

namespace Prism.Tests.Unit.Inference;

public sealed class InferenceRuntimeTests
{
    private readonly IRuntimeProviderResolver _resolver = Substitute.For<IRuntimeProviderResolver>();
    private readonly IInferenceRecorder _recorder = Substitute.For<IInferenceRecorder>();
    private readonly ITokenAnalysisService _analysisService = Substitute.For<ITokenAnalysisService>();
    private readonly ILogger<InferenceRuntime> _logger = Substitute.For<ILogger<InferenceRuntime>>();
    private readonly InferenceRuntime _sut;

    public InferenceRuntimeTests()
    {
        _sut = new InferenceRuntime(_resolver, _recorder, _analysisService, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ReturnsRunResult()
    {
        Guid instanceId = Guid.NewGuid();
        var request = new ChatRequest { Model = "test-model", Messages = [ChatMessage.User("Hello")] };
        var response = new ChatResponse { Content = "Hi there", ModelId = "test-model", FinishReason = "stop" };
        IInferenceProvider provider = CreateMockProvider(response);

        _resolver.ResolveAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(Result<IInferenceProvider>.Success(provider));
        _analysisService.Analyze(Arg.Any<LogprobsData?>(), Arg.Any<double>())
            .Returns(TokenAnalysis.Empty);
        _recorder.RecordAsync(Arg.Any<InferenceRecordData>(), Arg.Any<InferenceRunOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        Result<InferenceRunResult> result = await _sut.ExecuteAsync(instanceId, request, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Response.Content.Should().Be("Hi there");
        result.Value.RunId.Should().NotBeEmpty();
        result.Value.ProviderName.Should().Be("test-provider");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderNotFound_ReturnsFailure()
    {
        Guid instanceId = Guid.NewGuid();
        var request = new ChatRequest { Model = "test-model" };

        _resolver.ResolveAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(Result<IInferenceProvider>.Failure(Error.NotFound("Provider not found")));

        Result<InferenceRunResult> result = await _sut.ExecuteAsync(instanceId, request, null, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderFails_RecordsFailureAndReturnsError()
    {
        Guid instanceId = Guid.NewGuid();
        var request = new ChatRequest { Model = "test-model" };
        IInferenceProvider provider = CreateFailingProvider(Error.Internal("Inference failed"));

        _resolver.ResolveAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(Result<IInferenceProvider>.Success(provider));
        _recorder.RecordAsync(Arg.Any<InferenceRecordData>(), Arg.Any<InferenceRunOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        Result<InferenceRunResult> result = await _sut.ExecuteAsync(instanceId, request, null, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _recorder.Received(1).RecordAsync(
            Arg.Is<InferenceRecordData>(r => !r.IsSuccess),
            Arg.Any<InferenceRunOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RecordsRunAfterSuccess()
    {
        Guid instanceId = Guid.NewGuid();
        var request = new ChatRequest { Model = "test-model" };
        var response = new ChatResponse { Content = "result", ModelId = "test-model" };
        IInferenceProvider provider = CreateMockProvider(response);

        _resolver.ResolveAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(Result<IInferenceProvider>.Success(provider));
        _analysisService.Analyze(Arg.Any<LogprobsData?>(), Arg.Any<double>())
            .Returns(TokenAnalysis.Empty);
        _recorder.RecordAsync(Arg.Any<InferenceRecordData>(), Arg.Any<InferenceRunOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _sut.ExecuteAsync(instanceId, request, null, CancellationToken.None);

        await _recorder.Received(1).RecordAsync(
            Arg.Is<InferenceRecordData>(r => r.IsSuccess && r.ProviderName == "test-provider"),
            Arg.Any<InferenceRunOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsRecording_WhenOptionSet()
    {
        Guid instanceId = Guid.NewGuid();
        var request = new ChatRequest { Model = "test-model" };
        var options = new InferenceRunOptions { SkipRecording = true };
        var response = new ChatResponse { Content = "result", ModelId = "test-model" };
        IInferenceProvider provider = CreateMockProvider(response);

        _resolver.ResolveAsync(instanceId, Arg.Any<CancellationToken>())
            .Returns(Result<IInferenceProvider>.Success(provider));
        _analysisService.Analyze(Arg.Any<LogprobsData?>(), Arg.Any<double>())
            .Returns(TokenAnalysis.Empty);
        _recorder.RecordAsync(Arg.Any<InferenceRecordData>(), Arg.Any<InferenceRunOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _sut.ExecuteAsync(instanceId, request, options, CancellationToken.None);

        // The runtime still calls recorder; the recorder itself checks SkipRecording
        await _recorder.Received(1).RecordAsync(
            Arg.Any<InferenceRecordData>(),
            Arg.Is<InferenceRunOptions>(o => o.SkipRecording),
            Arg.Any<CancellationToken>());
    }

    private static IInferenceProvider CreateMockProvider(ChatResponse response)
    {
        var provider = Substitute.For<IInferenceProvider>();
        provider.ProviderName.Returns("test-provider");
        provider.Endpoint.Returns("http://localhost:8000");
        provider.Capabilities.Returns(new ProviderCapabilities());
        provider.ChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ChatResponse>.Success(response));
        return provider;
    }

    private static IInferenceProvider CreateFailingProvider(Error error)
    {
        var provider = Substitute.For<IInferenceProvider>();
        provider.ProviderName.Returns("test-provider");
        provider.Endpoint.Returns("http://localhost:8000");
        provider.Capabilities.Returns(new ProviderCapabilities());
        provider.ChatAsync(Arg.Any<ChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ChatResponse>.Failure(error));
        return provider;
    }
}

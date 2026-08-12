using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference;
using Prism.Features.Models.Application;
using Prism.Tests.Support;

namespace Prism.Tests.Unit.Inference;

/// <summary>
/// Proofs that wrapping a provider does not hide what it can do.
/// </summary>
/// <remarks>
/// Recording is applied at the single place providers are constructed, which is what makes
/// "every inference call is recorded" true. The wrapper implements <see cref="IInferenceProvider"/>
/// and nothing else, so every <c>provider is IHotReloadableProvider</c> test in the application
/// started answering false — and swapping the model on an Ollama, which supports it, replied
/// "Ollama does not support hot-swapping models". Nothing logged, nothing failed, the button
/// simply never worked.
/// </remarks>
public sealed class ProviderCapabilityVisibilityTests
{
    /// <summary>
    /// An Ollama built by the factory — recording and all — is still reachable as the
    /// hot-reloadable provider it is.
    /// </summary>
    [Fact]
    public void A_Recorded_Ollama_Is_Still_Hot_Reloadable()
    {
        IInferenceProvider provider = RecordingFactory().CreateProvider(
            "ollama", "http://localhost:11434", InferenceProviderType.Ollama);

        Assert.IsType<RecordingInferenceProvider>(provider);
        Assert.NotNull(provider.As<IHotReloadableProvider>());
    }

    /// <summary>
    /// The unwrapping does not invent capabilities: a provider that cannot hot-reload still
    /// reports that it cannot, wrapped or not.
    /// </summary>
    [Fact]
    public void A_Provider_Without_The_Capability_Still_Says_So()
    {
        IInferenceProvider provider = RecordingFactory().CreateProvider(
            "vllm", "http://localhost:8000", InferenceProviderType.Vllm);

        Assert.Null(provider.As<IHotReloadableProvider>());
    }

    /// <summary>
    /// Asking an unwrapped provider works the same way, so callers need only one form.
    /// </summary>
    [Fact]
    public void An_Unwrapped_Provider_Answers_The_Same_Question()
    {
        IInferenceProvider provider = new InferenceProviderFactory(
                FakeHttpTransport.ChatCompletion("x"), NullLoggerFactory.Instance)
            .CreateProvider("ollama", "http://localhost:11434", InferenceProviderType.Ollama);

        Assert.IsNotType<RecordingInferenceProvider>(provider);
        Assert.NotNull(provider.As<IHotReloadableProvider>());
    }

    /// <summary>
    /// A factory configured the way the application configures it: with a channel, so every
    /// provider it builds is wrapped for recording.
    /// </summary>
    private static InferenceProviderFactory RecordingFactory() =>
        new(FakeHttpTransport.ChatCompletion("x"),
            NullLoggerFactory.Instance,
            options: null,
            recordChannel: Channel.CreateUnbounded<InferenceRecordData>());
}

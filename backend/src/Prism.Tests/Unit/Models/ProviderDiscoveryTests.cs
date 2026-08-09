using Prism.Common.Inference;
using Prism.Features.Models.Application.DiscoverProviders;

namespace Prism.Tests.Unit.Models;

/// <summary>
/// Covers the guidance shown to someone setting Prism up for the first time.
/// </summary>
/// <remarks>
/// The text matters as much as the probe. A researcher who connects Ollama and then finds an
/// empty heatmap has been failed by the setup step, not by the heatmap — the capability
/// difference has to be stated before they choose, in terms of what it costs them.
/// </remarks>
public sealed class ProviderDiscoveryTests
{
    /// <summary>
    /// An Ollama too old for logprobs must say so, and must say that updating fixes it.
    /// </summary>
    /// <remarks>
    /// This note used to send people to vLLM. That was wrong twice over once Ollama gained
    /// logprobs in 0.12.11: it recommended replacing a server that would work after an update,
    /// and on an Apple Silicon Mac it recommended something that cannot run there at all, since
    /// vLLM needs CUDA. The remedy has to be one the reader can actually carry out.
    /// </remarks>
    [Fact]
    public void An_Old_Ollama_Is_Told_To_Update_Rather_Than_Replaced()
    {
        string note = DiscoverProvidersHandler.DescribeProvider(
            InferenceProviderType.Ollama, supportsLogprobs: false);

        Assert.Contains("per-token probabilities", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("empty", note, StringComparison.OrdinalIgnoreCase);

        // The remedy is an update, and it must not be "run vLLM" — see the remarks above.
        Assert.Contains("updating", note, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vLLM", note, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A capable provider must not carry the same warning, or the warning stops informing.
    /// </summary>
    [Fact]
    public void Vllm_Is_Described_As_Fully_Capable()
    {
        string note = DiscoverProvidersHandler.DescribeProvider(
            InferenceProviderType.Vllm, supportsLogprobs: true);

        Assert.DoesNotContain("empty", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("heatmap", note, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The description follows the probed capability, not the provider name. If a provider
    /// gains or loses logprob support, the guidance must move with it.
    /// </summary>
    [Theory]
    [InlineData(InferenceProviderType.OpenAiCompatible, true)]
    [InlineData(InferenceProviderType.OpenAiCompatible, false)]
    [InlineData(InferenceProviderType.LmStudio, false)]
    public void Guidance_Tracks_The_Capability_Not_The_Name(
        InferenceProviderType providerType, bool supportsLogprobs)
    {
        string note = DiscoverProvidersHandler.DescribeProvider(providerType, supportsLogprobs);

        bool warnsOfEmptyViews = note.Contains("empty", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(!supportsLogprobs, warnsOfEmptyViews);
    }
}

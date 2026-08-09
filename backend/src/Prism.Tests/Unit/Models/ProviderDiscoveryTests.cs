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
    /// Ollama's note must say plainly that the token-level views will not work, because that is
    /// the whole reason someone chose this tool.
    /// </summary>
    [Fact]
    public void Ollama_Is_Described_As_Lacking_Token_Probabilities()
    {
        string note = DiscoverProvidersHandler.DescribeProvider(
            InferenceProviderType.Ollama, supportsLogprobs: false);

        Assert.Contains("does not return per-token probabilities", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("empty", note, StringComparison.OrdinalIgnoreCase);

        // It must also say what to do about it rather than leaving the user stuck.
        Assert.Contains("vLLM", note, StringComparison.Ordinal);
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

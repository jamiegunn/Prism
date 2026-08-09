using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Inference.Providers;
using Prism.Features.Models.Domain;
using Prism.Features.Models.Infrastructure;

namespace Prism.Tests.Unit.Models;

/// <summary>
/// Keeps the development seed data honest about what each provider can do.
/// </summary>
/// <remarks>
/// <para>
/// The seeded instances are the first thing a new developer sees, and the UI trusts their
/// capability flags until someone presses Probe Capabilities. The Ollama seed claimed
/// <c>SupportsLogprobs = true</c>, which is false: the Playground therefore offered the
/// heatmap, entropy and surprise views, suppressed the warning that explains why they produce
/// nothing, and left the product looking broken rather than the provider looking limited.
/// </para>
/// <para>
/// Rather than pin the specific values — which would need editing every time a provider gains
/// a feature — these compare the seed against what the provider class itself declares. Drift in
/// either direction fails.
/// </para>
/// </remarks>
public sealed class SeededCapabilityTests
{
    /// <summary>
    /// Every seeded instance must claim exactly what its provider type declares.
    /// </summary>
    /// <param name="providerType">The provider being checked.</param>
    [Theory]
    [InlineData(InferenceProviderType.Ollama)]
    [InlineData(InferenceProviderType.Vllm)]
    public void Seeded_Capabilities_Match_The_Provider_They_Claim_To_Be(
        InferenceProviderType providerType)
    {
        InferenceInstance seeded = ModelsSeeder.SeedInstances()
            .Single(i => i.ProviderType == providerType);

        ProviderCapabilities declared = CapabilitiesFor(providerType);

        Assert.Equal(declared.SupportsLogprobs, seeded.SupportsLogprobs);
        Assert.Equal(declared.SupportsStreaming, seeded.SupportsStreaming);
        Assert.Equal(declared.SupportsTokenize, seeded.SupportsTokenize);
        Assert.Equal(declared.SupportsGuidedDecoding, seeded.SupportsGuidedDecoding);
        Assert.Equal(declared.SupportsMetrics, seeded.SupportsMetrics);
        Assert.Equal(declared.SupportsHotReload, seeded.SupportsModelSwap);
    }

    /// <summary>
    /// A provider without logprobs must not advertise a top-logprobs count.
    /// </summary>
    /// <remarks>
    /// The two fields disagreeing is what makes the slider appear with nothing behind it.
    /// </remarks>
    [Fact]
    public void A_Provider_Without_Logprobs_Offers_No_Alternatives()
    {
        foreach (InferenceInstance seeded in ModelsSeeder.SeedInstances())
        {
            if (!seeded.SupportsLogprobs)
            {
                Assert.Equal(0, seeded.MaxTopLogprobs);
            }
        }
    }

    /// <summary>
    /// Named so the failure message says which claim was wrong.
    /// </summary>
    [Fact]
    public void Ollama_Does_Not_Claim_Token_Probabilities()
    {
        InferenceInstance ollama = ModelsSeeder.SeedInstances()
            .Single(i => i.ProviderType == InferenceProviderType.Ollama);

        Assert.False(
            ollama.SupportsLogprobs,
            "Ollama's API returns no per-token probabilities. Seeding it as though it does "
            + "makes the Playground offer views that silently render nothing.");
    }

    /// <summary>
    /// Reads the capabilities a provider declares about itself.
    /// </summary>
    /// <param name="type">The provider kind.</param>
    /// <returns>Its declared capabilities.</returns>
    /// <remarks>
    /// The flags are set inline on the property and do not depend on the constructor arguments,
    /// so a throwaway client and endpoint are enough to read them.
    /// </remarks>
    private static ProviderCapabilities CapabilitiesFor(InferenceProviderType type)
    {
        using var http = new HttpClient();
        const string endpoint = "http://localhost:1";

        return type switch
        {
            InferenceProviderType.Ollama =>
                new OllamaProvider(http, "probe", endpoint, NullLogger<OllamaProvider>.Instance)
                    .Capabilities,
            InferenceProviderType.Vllm =>
                new VllmProvider(http, "probe", endpoint, NullLogger<VllmProvider>.Instance)
                    .Capabilities,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }
}

using Prism.Common.Inference.Capabilities;

namespace Prism.Tests.Unit.Capabilities;

public sealed class CapabilityTierTests
{
    [Fact]
    public void ResearchTier_WhenAllCapabilities()
    {
        var snapshot = new ProviderCapabilitySnapshot
        {
            InstanceId = Guid.NewGuid(),
            ProviderName = "vllm-research",
            Tier = CapabilityTier.Research,
            SupportsLogprobs = true,
            MaxLogprobs = 20,
            SupportsTokenize = true,
            SupportsGuidedDecoding = true,
            SupportsStreaming = true,
            SupportsMetrics = true,
            ProbeSucceeded = true,
            ProbedAt = DateTime.UtcNow
        };

        snapshot.Tier.Should().Be(CapabilityTier.Research);
        snapshot.SupportsLogprobs.Should().BeTrue();
        snapshot.SupportsTokenize.Should().BeTrue();
        snapshot.SupportsGuidedDecoding.Should().BeTrue();
        snapshot.SupportsMetrics.Should().BeTrue();
    }

    [Fact]
    public void InspectTier_WhenLogprobsOnly()
    {
        var snapshot = new ProviderCapabilitySnapshot
        {
            InstanceId = Guid.NewGuid(),
            ProviderName = "openai-compat",
            Tier = CapabilityTier.Inspect,
            SupportsLogprobs = true,
            MaxLogprobs = 5,
            SupportsTokenize = false,
            SupportsGuidedDecoding = false,
            SupportsStreaming = true,
            SupportsMetrics = false,
            ProbeSucceeded = true,
            ProbedAt = DateTime.UtcNow
        };

        snapshot.Tier.Should().Be(CapabilityTier.Inspect);
        snapshot.SupportsLogprobs.Should().BeTrue();
        snapshot.SupportsTokenize.Should().BeFalse();
    }

    [Fact]
    public void InspectTier_WhenTokenizeOnly()
    {
        var snapshot = new ProviderCapabilitySnapshot
        {
            InstanceId = Guid.NewGuid(),
            ProviderName = "tokenizer-provider",
            Tier = CapabilityTier.Inspect,
            SupportsLogprobs = false,
            SupportsTokenize = true,
            SupportsGuidedDecoding = false,
            SupportsStreaming = true,
            SupportsMetrics = false,
            ProbeSucceeded = true,
            ProbedAt = DateTime.UtcNow
        };

        snapshot.Tier.Should().Be(CapabilityTier.Inspect);
        snapshot.SupportsTokenize.Should().BeTrue();
        snapshot.SupportsLogprobs.Should().BeFalse();
    }

    [Fact]
    public void ChatTier_WhenNoResearchCapabilities()
    {
        var snapshot = new ProviderCapabilitySnapshot
        {
            InstanceId = Guid.NewGuid(),
            ProviderName = "basic-chat",
            Tier = CapabilityTier.Chat,
            SupportsLogprobs = false,
            SupportsTokenize = false,
            SupportsGuidedDecoding = false,
            SupportsStreaming = true,
            SupportsMetrics = false,
            ProbeSucceeded = true,
            ProbedAt = DateTime.UtcNow
        };

        snapshot.Tier.Should().Be(CapabilityTier.Chat);
        snapshot.SupportsLogprobs.Should().BeFalse();
        snapshot.SupportsTokenize.Should().BeFalse();
        snapshot.SupportsGuidedDecoding.Should().BeFalse();
        snapshot.SupportsMetrics.Should().BeFalse();
    }

    [Fact]
    public void UnknownTier_WhenProbeFailed()
    {
        var snapshot = new ProviderCapabilitySnapshot
        {
            InstanceId = Guid.NewGuid(),
            ProviderName = "unreachable",
            Tier = CapabilityTier.Unknown,
            ProbeSucceeded = false,
            ProbeError = "Connection refused",
            ProbedAt = DateTime.UtcNow
        };

        snapshot.Tier.Should().Be(CapabilityTier.Unknown);
        snapshot.ProbeSucceeded.Should().BeFalse();
        snapshot.ProbeError.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(CapabilityTier.Unknown, 0)]
    [InlineData(CapabilityTier.Chat, 1)]
    [InlineData(CapabilityTier.Inspect, 2)]
    [InlineData(CapabilityTier.Research, 3)]
    public void TierOrdering_HigherTiersHaveHigherValues(CapabilityTier tier, int expectedValue)
    {
        ((int)tier).Should().Be(expectedValue);
    }
}

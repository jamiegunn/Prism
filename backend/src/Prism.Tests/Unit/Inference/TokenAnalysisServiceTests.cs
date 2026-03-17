using Prism.Common.Inference.Models;
using Prism.Common.Inference.Runtime;

namespace Prism.Tests.Unit.Inference;

public sealed class TokenAnalysisServiceTests
{
    private readonly TokenAnalysisService _sut = new();

    [Fact]
    public void Analyze_WithNullLogprobsData_ReturnsEmpty()
    {
        TokenAnalysis result = _sut.Analyze(null);

        result.HasData.Should().BeFalse();
        result.TokenCount.Should().Be(0);
        result.Perplexity.Should().Be(0);
        result.SurpriseTokens.Should().BeEmpty();
        result.EntropyPerToken.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_WithEmptyTokens_ReturnsEmpty()
    {
        var logprobsData = new LogprobsData { Tokens = [] };

        TokenAnalysis result = _sut.Analyze(logprobsData);

        result.HasData.Should().BeFalse();
        result.TokenCount.Should().Be(0);
    }

    [Fact]
    public void Analyze_WithSingleToken_ComputesCorrectly()
    {
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob
                {
                    Token = "Paris",
                    Logprob = -0.02,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " Paris", Logprob = -0.02 },
                        new TopLogprob { Token = " Lyon", Logprob = -5.8 }
                    ]
                }
            ]
        };

        TokenAnalysis result = _sut.Analyze(logprobsData);

        result.HasData.Should().BeTrue();
        result.TokenCount.Should().Be(1);
        result.Perplexity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Analyze_WithMultipleTokens_ComputesPerplexity()
    {
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob { Token = "The", Logprob = -0.05 },
                new TokenLogprob { Token = " capital", Logprob = -0.12 },
                new TokenLogprob { Token = " is", Logprob = -0.02 }
            ]
        };

        TokenAnalysis result = _sut.Analyze(logprobsData);

        // Perplexity = exp(average(-logprobs)) = exp((0.05 + 0.12 + 0.02) / 3) = exp(0.0633...)
        double expectedPerplexity = Math.Exp((-logprobsData.Tokens[0].Logprob
            - logprobsData.Tokens[1].Logprob
            - logprobsData.Tokens[2].Logprob) / 3.0);

        result.Perplexity.Should().BeApproximately(expectedPerplexity, 0.001);
        result.TokenCount.Should().Be(3);
    }

    [Fact]
    public void Analyze_FindsSurpriseTokens_BelowThreshold()
    {
        // probability = exp(-2.9957) ≈ 0.05
        double logprob = Math.Log(0.05);
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob { Token = "unexpected", Logprob = logprob }
            ]
        };

        TokenAnalysis result = _sut.Analyze(logprobsData, surpriseThreshold: 0.1);

        result.SurpriseTokens.Should().HaveCount(1);
        result.SurpriseTokens[0].Token.Should().Be("unexpected");
        result.SurpriseTokens[0].Probability.Should().BeApproximately(0.05, 0.001);
    }

    [Fact]
    public void Analyze_NoSurpriseTokens_AboveThreshold()
    {
        // probability = exp(-0.0513) ≈ 0.95
        double logprob = Math.Log(0.95);
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob { Token = "expected", Logprob = logprob }
            ]
        };

        TokenAnalysis result = _sut.Analyze(logprobsData, surpriseThreshold: 0.1);

        result.SurpriseTokens.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_ComputesEntropyPerToken()
    {
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob
                {
                    Token = "A",
                    Logprob = -0.1,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = "A", Logprob = -0.1 },
                        new TopLogprob { Token = "B", Logprob = -2.3 }
                    ]
                },
                new TokenLogprob
                {
                    Token = "C",
                    Logprob = -0.5,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = "C", Logprob = -0.5 },
                        new TopLogprob { Token = "D", Logprob = -1.2 }
                    ]
                }
            ]
        };

        TokenAnalysis result = _sut.Analyze(logprobsData);

        result.EntropyPerToken.Should().HaveCount(2);
        result.MeanEntropy.Should().BeApproximately(
            (result.EntropyPerToken[0] + result.EntropyPerToken[1]) / 2.0, 0.0001);
    }
}

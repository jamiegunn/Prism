using Prism.Common.Inference.Metrics;
using Prism.Common.Inference.Models;

namespace Prism.Tests.Unit.Inference;

public sealed class LogprobsCalculatorTests
{
    [Fact]
    public void CalculatePerplexity_WithValidData_ReturnsCorrectValue()
    {
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob { Token = "a", Logprob = -0.05 },
                new TokenLogprob { Token = "b", Logprob = -0.10 },
                new TokenLogprob { Token = "c", Logprob = -0.15 }
            ]
        };

        double result = LogprobsCalculator.CalculatePerplexity(logprobsData);

        // exp(average(0.05, 0.10, 0.15)) = exp(0.10) ≈ 1.10517
        result.Should().BeApproximately(Math.Exp(0.10), 0.001);
    }

    [Fact]
    public void CalculatePerplexity_WithEmptyData_ReturnsZero()
    {
        var logprobsData = new LogprobsData { Tokens = [] };

        double result = LogprobsCalculator.CalculatePerplexity(logprobsData);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateEntropy_WithUniformDistribution_ReturnsMaxEntropy()
    {
        // 4 equal probabilities: log(0.25) = -1.3863
        double logQuarter = Math.Log(0.25);
        var tokenLogprob = new TokenLogprob
        {
            Token = "x",
            Logprob = logQuarter,
            TopLogprobs =
            [
                new TopLogprob { Token = "a", Logprob = logQuarter },
                new TopLogprob { Token = "b", Logprob = logQuarter },
                new TopLogprob { Token = "c", Logprob = logQuarter },
                new TopLogprob { Token = "d", Logprob = logQuarter }
            ]
        };

        double result = LogprobsCalculator.CalculateEntropy(tokenLogprob);

        // Entropy in nats: -4 * 0.25 * ln(0.25) = ln(4) ≈ 1.3863
        double expectedEntropy = Math.Log(4);
        result.Should().BeApproximately(expectedEntropy, 0.001);
    }

    [Fact]
    public void CalculateEntropy_WithCertainDistribution_ReturnsZero()
    {
        var tokenLogprob = new TokenLogprob
        {
            Token = "sure",
            Logprob = 0.0, // log(1.0) = 0
            TopLogprobs =
            [
                new TopLogprob { Token = "sure", Logprob = 0.0 }
            ]
        };

        double result = LogprobsCalculator.CalculateEntropy(tokenLogprob);

        // -1.0 * log(1.0) = 0
        result.Should().BeApproximately(0.0, 0.0001);
    }

    [Fact]
    public void FindSurpriseTokens_IdentifiesLowProbTokens()
    {
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob { Token = "confident", Logprob = Math.Log(0.95) },
                new TokenLogprob { Token = "surprised", Logprob = Math.Log(0.03) },
                new TokenLogprob { Token = "normal", Logprob = Math.Log(0.80) }
            ]
        };

        IReadOnlyList<TokenLogprob> result = LogprobsCalculator.FindSurpriseTokens(logprobsData, threshold: 0.1);

        result.Should().HaveCount(1);
        result[0].Token.Should().Be("surprised");
    }

    [Fact]
    public void CalculateAverageLogprob_ReturnsCorrectMean()
    {
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob { Token = "a", Logprob = -0.10 },
                new TokenLogprob { Token = "b", Logprob = -0.20 },
                new TokenLogprob { Token = "c", Logprob = -0.30 }
            ]
        };

        double result = LogprobsCalculator.CalculateAverageLogprob(logprobsData);

        result.Should().BeApproximately(-0.20, 0.0001);
    }

    [Fact]
    public void CalculateAverageLogprob_WithEmptyData_ReturnsZero()
    {
        var logprobsData = new LogprobsData { Tokens = [] };

        double result = LogprobsCalculator.CalculateAverageLogprob(logprobsData);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateEntropyPerToken_ReturnsOneEntryPerToken()
    {
        var logprobsData = new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob
                {
                    Token = "x",
                    Logprob = -0.5,
                    TopLogprobs = [new TopLogprob { Token = "x", Logprob = -0.5 }]
                },
                new TokenLogprob
                {
                    Token = "y",
                    Logprob = -1.0,
                    TopLogprobs = [new TopLogprob { Token = "y", Logprob = -1.0 }]
                }
            ]
        };

        IReadOnlyList<double> result = LogprobsCalculator.CalculateEntropyPerToken(logprobsData);

        result.Should().HaveCount(2);
    }
}

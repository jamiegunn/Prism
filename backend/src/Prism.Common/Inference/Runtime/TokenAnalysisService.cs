using Prism.Common.Inference.Metrics;
using Prism.Common.Inference.Models;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Default implementation of <see cref="ITokenAnalysisService"/>.
/// Delegates to <see cref="LogprobsCalculator"/> for core computations and
/// assembles the results into a unified <see cref="TokenAnalysis"/> record.
/// </summary>
public sealed class TokenAnalysisService : ITokenAnalysisService
{
    /// <inheritdoc />
    public TokenAnalysis Analyze(LogprobsData? logprobsData, double surpriseThreshold = 0.1)
    {
        if (logprobsData is null || logprobsData.Tokens.Count == 0)
        {
            return TokenAnalysis.Empty;
        }

        double perplexity = LogprobsCalculator.CalculatePerplexity(logprobsData);
        double averageLogprob = LogprobsCalculator.CalculateAverageLogprob(logprobsData);
        IReadOnlyList<double> entropyPerToken = LogprobsCalculator.CalculateEntropyPerToken(logprobsData);
        IReadOnlyList<TokenLogprob> rawSurpriseTokens = LogprobsCalculator.FindSurpriseTokens(logprobsData, surpriseThreshold);

        double meanEntropy = entropyPerToken.Count > 0
            ? entropyPerToken.Average()
            : 0.0;

        List<SurpriseToken> surpriseTokens = [];
        foreach (TokenLogprob tokenLogprob in rawSurpriseTokens)
        {
            int position = logprobsData.Tokens.IndexOf(tokenLogprob);
            double entropy = position >= 0 && position < entropyPerToken.Count
                ? entropyPerToken[position]
                : 0.0;

            surpriseTokens.Add(new SurpriseToken
            {
                Token = tokenLogprob.Token,
                Logprob = tokenLogprob.Logprob,
                Probability = tokenLogprob.Probability,
                Position = position,
                Entropy = entropy
            });
        }

        return new TokenAnalysis
        {
            Perplexity = perplexity,
            AverageLogprob = averageLogprob,
            EntropyPerToken = entropyPerToken,
            MeanEntropy = meanEntropy,
            SurpriseTokens = surpriseTokens,
            TokenCount = logprobsData.Tokens.Count,
            HasData = true
        };
    }
}

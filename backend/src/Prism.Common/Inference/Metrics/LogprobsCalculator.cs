using Prism.Common.Inference.Models;

namespace Prism.Common.Inference.Metrics;

/// <summary>
/// Provides static methods for computing statistical measures from log probability data.
/// Used for analyzing model confidence, uncertainty, and token-level surprise.
/// </summary>
public static class LogprobsCalculator
{
    /// <summary>
    /// Calculates the perplexity of a sequence from its log probabilities.
    /// Perplexity is exp(average negative log probability). Lower values indicate
    /// higher model confidence in the generated sequence.
    /// </summary>
    /// <param name="logprobsData">The log probabilities data for the sequence.</param>
    /// <returns>The perplexity value, or 0 if no tokens are present.</returns>
    /// <example>
    /// A perplexity of 1.0 means the model was perfectly confident.
    /// A perplexity of 10.0 means the model was, on average, equally uncertain between 10 choices.
    /// </example>
    public static double CalculatePerplexity(LogprobsData logprobsData)
    {
        if (logprobsData.Tokens.Count == 0)
        {
            return 0;
        }

        double averageNegLogprob = logprobsData.Tokens.Average(t => -t.Logprob);
        return Math.Exp(averageNegLogprob);
    }

    /// <summary>
    /// Calculates the Shannon entropy for a single token position from its top log probabilities.
    /// Higher entropy indicates more uncertainty about the token choice at this position.
    /// </summary>
    /// <param name="tokenLogprob">The log probability entry for a single token position.</param>
    /// <returns>The entropy value in nats. Returns 0 if no top logprobs are available.</returns>
    public static double CalculateEntropy(TokenLogprob tokenLogprob)
    {
        if (tokenLogprob.TopLogprobs.Count == 0)
        {
            return 0;
        }

        double entropy = 0;
        foreach (TopLogprob topLogprob in tokenLogprob.TopLogprobs)
        {
            double probability = topLogprob.Probability;
            if (probability > 0)
            {
                entropy -= probability * Math.Log(probability);
            }
        }

        return entropy;
    }

    /// <summary>
    /// Calculates the Shannon entropy at each token position in the sequence.
    /// </summary>
    /// <param name="logprobsData">The log probabilities data for the sequence.</param>
    /// <returns>A list of entropy values, one per token position.</returns>
    public static IReadOnlyList<double> CalculateEntropyPerToken(LogprobsData logprobsData)
    {
        return logprobsData.Tokens
            .Select(CalculateEntropy)
            .ToList();
    }

    /// <summary>
    /// Finds tokens in the sequence whose probability falls below a specified threshold.
    /// These "surprise" tokens indicate positions where the model was uncertain or the
    /// text was unexpected.
    /// </summary>
    /// <param name="logprobsData">The log probabilities data for the sequence.</param>
    /// <param name="threshold">The probability threshold below which a token is considered surprising (0.0 to 1.0).</param>
    /// <returns>A list of token log probability entries whose probability is below the threshold.</returns>
    public static IReadOnlyList<TokenLogprob> FindSurpriseTokens(LogprobsData logprobsData, double threshold)
    {
        return logprobsData.Tokens
            .Where(t => t.Probability < threshold)
            .ToList();
    }

    /// <summary>
    /// Calculates the mean log probability across all tokens in the sequence.
    /// </summary>
    /// <param name="logprobsData">The log probabilities data for the sequence.</param>
    /// <returns>The average log probability, or 0 if no tokens are present.</returns>
    public static double CalculateAverageLogprob(LogprobsData logprobsData)
    {
        if (logprobsData.Tokens.Count == 0)
        {
            return 0;
        }

        return logprobsData.Tokens.Average(t => t.Logprob);
    }
}

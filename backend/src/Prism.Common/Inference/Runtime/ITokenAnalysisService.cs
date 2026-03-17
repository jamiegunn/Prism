using Prism.Common.Inference.Models;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Computes token-level analysis metrics from logprobs data.
/// Centralizes all entropy, perplexity, surprise, and calibration computations
/// so that every feature gets identical analysis results.
/// </summary>
public interface ITokenAnalysisService
{
    /// <summary>
    /// Analyzes logprobs data and produces a complete <see cref="TokenAnalysis"/>.
    /// </summary>
    /// <param name="logprobsData">The logprobs data from a chat response. May be null.</param>
    /// <param name="surpriseThreshold">Probability threshold below which tokens are flagged as surprising.</param>
    /// <returns>A <see cref="TokenAnalysis"/> with computed metrics, or <see cref="TokenAnalysis.Empty"/> if no data.</returns>
    TokenAnalysis Analyze(LogprobsData? logprobsData, double surpriseThreshold = 0.1);
}

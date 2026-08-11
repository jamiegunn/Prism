namespace Prism.Features.Evaluation.Domain;

/// <summary>
/// Expected Calibration Error and Brier score, computed exactly as their definitions state —
/// both are short enough that a dependency would be a liability, and both are proved by
/// hand-computed fixtures and invariants in <c>CalibrationMetricsTests</c>.
/// </summary>
public static class CalibrationMetrics
{
    /// <summary>
    /// The bin count every ECE Prism reports uses. ECE changes with the bin count, so the
    /// count is part of the definition and is displayed with the number.
    /// </summary>
    public const int DefaultBinCount = 10;

    /// <summary>
    /// One prediction with its outcome.
    /// </summary>
    /// <param name="Confidence">The model's confidence in [0, 1] — here, the geometric mean
    /// of the chosen tokens' probabilities.</param>
    /// <param name="IsCorrect">Whether the answer was correct.</param>
    public readonly record struct Prediction(double Confidence, bool IsCorrect);

    /// <summary>
    /// Computes Expected Calibration Error over equal-width bins:
    /// <c>ECE = Σ_b (n_b / N) · |accuracy(b) − meanConfidence(b)|</c>, with a prediction at
    /// confidence <c>c</c> assigned to bin <c>min(⌊c·B⌋, B−1)</c>. Empty bins contribute
    /// nothing. Returns null for an empty input: the calibration of no predictions is
    /// absent, not zero.
    /// </summary>
    /// <param name="predictions">The predictions.</param>
    /// <param name="binCount">The number of equal-width bins.</param>
    /// <returns>The ECE in [0, 1], or null when there are no predictions.</returns>
    public static double? ExpectedCalibrationError(
        IReadOnlyList<Prediction> predictions, int binCount = DefaultBinCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(binCount, 1);

        if (predictions.Count == 0)
        {
            return null;
        }

        int[] counts = new int[binCount];
        double[] confidenceSums = new double[binCount];
        int[] correctCounts = new int[binCount];

        foreach (Prediction p in predictions)
        {
            int bin = Math.Min((int)(p.Confidence * binCount), binCount - 1);
            counts[bin]++;
            confidenceSums[bin] += p.Confidence;
            correctCounts[bin] += p.IsCorrect ? 1 : 0;
        }

        double ece = 0.0;

        for (int b = 0; b < binCount; b++)
        {
            if (counts[b] == 0)
            {
                continue;
            }

            double accuracy = (double)correctCounts[b] / counts[b];
            double meanConfidence = confidenceSums[b] / counts[b];

            ece += (double)counts[b] / predictions.Count * Math.Abs(accuracy - meanConfidence);
        }

        return ece;
    }

    /// <summary>
    /// Computes the Brier score: the mean squared error between confidence and the 0/1
    /// outcome, <c>mean((c_i − y_i)²)</c>, bounded in [0, 1]. Returns null for an empty
    /// input.
    /// </summary>
    /// <param name="predictions">The predictions.</param>
    /// <returns>The Brier score in [0, 1], or null when there are no predictions.</returns>
    public static double? BrierScore(IReadOnlyList<Prediction> predictions)
    {
        if (predictions.Count == 0)
        {
            return null;
        }

        double sum = 0.0;

        foreach (Prediction p in predictions)
        {
            double label = p.IsCorrect ? 1.0 : 0.0;
            double diff = p.Confidence - label;
            sum += diff * diff;
        }

        return sum / predictions.Count;
    }
}

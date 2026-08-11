using Prism.Features.Evaluation.Application.GetCalibration;
using Prism.Features.Evaluation.Domain;
using Pred = Prism.Features.Evaluation.Domain.CalibrationMetrics.Prediction;

namespace Prism.Tests.Unit.Evaluation;

/// <summary>
/// Proofs for ECE and Brier: a fixture whose ECE is worked out by hand in the comments and
/// asserted exactly, plus the invariants that hold for all inputs.
/// </summary>
public sealed class CalibrationMetricsTests
{
    private const double Tolerance = 1e-12;

    /// <summary>
    /// Ten predictions across three occupied bins (of 10 equal-width bins), ECE worked by
    /// hand:
    ///
    /// Bin [0.2, 0.3): predictions (0.20, T), (0.25, F), (0.25, F)
    ///   n=3, mean conf = 0.70/3 = 0.2333…, accuracy = 1/3 = 0.3333…
    ///   |acc − conf| = 0.10 exactly (1/3 − 7/30 = 10/30 − 7/30 = 3/30).
    /// Bin [0.5, 0.6): predictions (0.50, T), (0.55, F), (0.55, T), (0.50, F)
    ///   n=4, mean conf = 2.10/4 = 0.525, accuracy = 2/4 = 0.5, |diff| = 0.025.
    /// Bin [0.9, 1.0]: predictions (0.90, T), (0.95, T), (1.00, T) — 1.00 clamps into bin 9
    ///   n=3, mean conf = 2.85/3 = 0.95, accuracy = 3/3 = 1, |diff| = 0.05.
    ///
    /// ECE = (3/10)(3/30) + (4/10)(0.025) + (3/10)(0.05)
    ///     = 0.03 + 0.01 + 0.015 = 0.055 exactly.
    ///
    /// Brier = mean of (c − y)²:
    ///   (0.20−1)² = 0.64;  (0.25−0)² = 0.0625 ×2;  (0.50−1)² = 0.25;  (0.55−0)² = 0.3025;
    ///   (0.55−1)² = 0.2025; (0.50−0)² = 0.25; (0.90−1)² = 0.01; (0.95−1)² = 0.0025;
    ///   (1.00−1)² = 0.
    ///   Sum = 0.64 + 0.125 + 0.25 + 0.3025 + 0.2025 + 0.25 + 0.01 + 0.0025 + 0
    ///       = 1.7825 → Brier = 0.17825 exactly.
    /// </summary>
    [Fact]
    public void Ece_And_Brier_Match_The_Hand_Computed_Fixture()
    {
        Pred[] predictions =
        [
            new(0.20, true), new(0.25, false), new(0.25, false),
            new(0.50, true), new(0.55, false), new(0.55, true), new(0.50, false),
            new(0.90, true), new(0.95, true), new(1.00, true),
        ];

        double? ece = CalibrationMetrics.ExpectedCalibrationError(predictions);
        double? brier = CalibrationMetrics.BrierScore(predictions);

        Assert.NotNull(ece);
        Assert.NotNull(brier);
        Assert.Equal(0.055, ece!.Value, Tolerance);
        Assert.Equal(0.17825, brier!.Value, Tolerance);
    }

    /// <summary>
    /// A perfectly calibrated set — every bin's accuracy equals its mean confidence — has
    /// ECE exactly 0. Constructed: bin [0.2, 0.3) holds five predictions at 0.2 with exactly
    /// one correct (accuracy 0.2); bin [0.8, 0.9) holds five at 0.8 with four correct.
    /// </summary>
    [Fact]
    public void Perfectly_Calibrated_Predictions_Have_Ece_Zero()
    {
        Pred[] predictions =
        [
            new(0.2, true), new(0.2, false), new(0.2, false), new(0.2, false), new(0.2, false),
            new(0.8, true), new(0.8, true), new(0.8, true), new(0.8, true), new(0.8, false),
        ];

        Assert.Equal(0.0, CalibrationMetrics.ExpectedCalibrationError(predictions)!.Value, Tolerance);
    }

    /// <summary>
    /// A maximally over-confident set — every prediction at confidence 1.0, every one wrong —
    /// has ECE exactly 1 and Brier exactly 1.
    /// </summary>
    [Fact]
    public void Maximally_Overconfident_Predictions_Have_Ece_And_Brier_One()
    {
        Pred[] predictions = Enumerable.Repeat(new Pred(1.0, false), 7).ToArray();

        Assert.Equal(1.0, CalibrationMetrics.ExpectedCalibrationError(predictions)!.Value, Tolerance);
        Assert.Equal(1.0, CalibrationMetrics.BrierScore(predictions)!.Value, Tolerance);
    }

    /// <summary>
    /// Invariants over generated inputs: ECE and Brier are bounded in [0, 1]; Brier equals
    /// the mean squared error against the 0/1 label by definition; both are permutation
    /// invariant; and the empty set yields null — absent, never zero.
    /// </summary>
    [Fact]
    public void Bounds_Permutation_Invariance_And_Empty_Behaviour()
    {
        var random = new Random(20260811);

        for (int trial = 0; trial < 100; trial++)
        {
            Pred[] predictions = Enumerable.Range(0, random.Next(1, 50))
                .Select(_ => new Pred(random.NextDouble(), random.Next(2) == 0))
                .ToArray();

            double ece = CalibrationMetrics.ExpectedCalibrationError(predictions)!.Value;
            double brier = CalibrationMetrics.BrierScore(predictions)!.Value;

            Assert.InRange(ece, 0.0, 1.0);
            Assert.InRange(brier, 0.0, 1.0);

            // Brier is definitionally MSE against the label.
            double mse = predictions.Average(p =>
            {
                double diff = p.Confidence - (p.IsCorrect ? 1.0 : 0.0);
                return diff * diff;
            });
            Assert.Equal(mse, brier, Tolerance);

            // Shuffling the predictions changes nothing.
            Pred[] shuffled = predictions.OrderBy(_ => random.Next()).ToArray();
            Assert.Equal(ece, CalibrationMetrics.ExpectedCalibrationError(shuffled)!.Value, Tolerance);
            Assert.Equal(brier, CalibrationMetrics.BrierScore(shuffled)!.Value, Tolerance);
        }

        Assert.Null(CalibrationMetrics.ExpectedCalibrationError([]));
        Assert.Null(CalibrationMetrics.BrierScore([]));
    }

    /// <summary>
    /// ECE depends on the bin count — which is why the bin count is part of the reported
    /// definition. The fixture's ECE at 10 bins (0.055) differs from its ECE at 2 bins.
    /// Hand-computed at 2 bins: bin [0, 0.5) holds (0.20,T),(0.25,F),(0.25,F) → n=3,
    /// conf 7/30, acc 1/3, diff 3/30; bin [0.5, 1] holds the other 7 → conf 4.95/7,
    /// acc 5/7, diff = |5/7 − 4.95/7| = 0.05/7. ECE = 0.3·0.1 + 0.7·(0.05/7) = 0.035.
    /// </summary>
    [Fact]
    public void Ece_Changes_With_Bin_Count()
    {
        Pred[] predictions =
        [
            new(0.20, true), new(0.25, false), new(0.25, false),
            new(0.50, true), new(0.55, false), new(0.55, true), new(0.50, false),
            new(0.90, true), new(0.95, true), new(1.00, true),
        ];

        double at10 = CalibrationMetrics.ExpectedCalibrationError(predictions, 10)!.Value;
        double at2 = CalibrationMetrics.ExpectedCalibrationError(predictions, 2)!.Value;

        Assert.Equal(0.055, at10, Tolerance);
        Assert.Equal(0.035, at2, Tolerance);
        Assert.NotEqual(at10, at2);
    }

    /// <summary>
    /// Sequence confidence comes from the chosen token's logprob — not the top-1
    /// alternative's — and is exp(mean logprob). A two-token answer with logprobs −0.5 and
    /// −1.5 has confidence exp(−1.0), even when the stored top alternatives are more probable
    /// (the sampler picked a non-argmax token).
    /// </summary>
    [Fact]
    public void Confidence_Uses_The_Chosen_Tokens_Probability()
    {
        // topLogprobs list a MORE probable alternative (-0.1) than the chosen token; a wrong
        // implementation that reads top-1 would compute exp(-0.1) instead.
        const string logprobsJson = """
            {
              "tokens": [
                { "token": "a", "logprob": -0.5, "topLogprobs": [ { "token": "x", "logprob": -0.1 } ] },
                { "token": "b", "logprob": -1.5, "topLogprobs": [ { "token": "y", "logprob": -0.1 } ] }
              ]
            }
            """;

        double? confidence = GetCalibrationHandler.SequenceConfidence(logprobsJson);

        Assert.NotNull(confidence);
        Assert.Equal(Math.Exp(-1.0), confidence!.Value, Tolerance);

        // Absence is null, never zero.
        Assert.Null(GetCalibrationHandler.SequenceConfidence(null));
        Assert.Null(GetCalibrationHandler.SequenceConfidence(""));
        Assert.Null(GetCalibrationHandler.SequenceConfidence("""{"tokens":[]}"""));
        Assert.Null(GetCalibrationHandler.SequenceConfidence("not json"));
    }
}

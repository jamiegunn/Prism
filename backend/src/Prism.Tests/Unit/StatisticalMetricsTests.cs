using Prism.Features.Evaluation.Domain;

namespace Prism.Tests.Unit;

/// <summary>
/// Differential tests for <see cref="StatisticalMetrics"/> against scipy.stats reference
/// values (scipy 1.17.1: <c>t.ppf</c>, <c>t.cdf</c>, <c>t.interval</c>, <c>ttest_rel</c>),
/// plus the invariants that make an interval an interval.
/// </summary>
public sealed class StatisticalMetricsTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// The t quantile matches scipy.stats.t.ppf across degrees of freedom, including the
    /// notorious df=1 (Cauchy) case where the 97.5% point is 12.7, not 1.96.
    /// </summary>
    /// <param name="p">The probability.</param>
    /// <param name="df">Degrees of freedom.</param>
    /// <param name="expected">scipy.stats.t.ppf(p, df).</param>
    [Theory]
    [InlineData(0.975, 1, 12.706204736175)]
    [InlineData(0.975, 4, 2.776445105198)]
    [InlineData(0.975, 9, 2.262157162798)]
    [InlineData(0.975, 29, 2.045229642133)]
    [InlineData(0.995, 9, 3.249835541592)]
    [InlineData(0.6, 3, 0.276670662333)]
    [InlineData(0.975, 999, 1.962341461133)]
    [InlineData(0.975, 2000, 1.961150826099)]
    public void Quantile_Matches_Scipy(double p, double df, double expected)
    {
        Assert.Equal(expected, StatisticalMetrics.StudentTQuantile(p, df), Tolerance);
    }

    /// <summary>
    /// The t CDF matches scipy.stats.t.cdf, on both sides of zero and at zero.
    /// </summary>
    /// <param name="t">The evaluation point.</param>
    /// <param name="df">Degrees of freedom.</param>
    /// <param name="expected">scipy.stats.t.cdf(t, df).</param>
    [Theory]
    [InlineData(2.262157162740992, 9, 0.974999999998)]
    [InlineData(1.0, 5, 0.818391266175)]
    [InlineData(-1.5, 7, 0.088649243495)]
    [InlineData(0.0, 3, 0.5)]
    [InlineData(3.7, 12, 0.998482114518)]
    public void Cdf_Matches_Scipy(double t, double df, double expected)
    {
        Assert.Equal(expected, StatisticalMetrics.StudentTCdf(t, df), Tolerance);
    }

    /// <summary>
    /// Deep-tail honesty (adversarial pass 1): an extreme t statistic yields a p-value that
    /// is tiny but correct — scipy.stats.t.sf(50, 10)*2 = 2.474310329303e-13 — rather than a
    /// rounded 0 or a cancellation artefact. This test caught a real defect: computing the
    /// p-value as 2·(1 − CDF) kept only ~3 significant digits here; the survival function is
    /// now evaluated directly.
    /// </summary>
    [Fact]
    public void Survival_Function_Keeps_Precision_In_The_Deep_Tail()
    {
        double p = 2.0 * StatisticalMetrics.StudentTSurvival(50, 10);
        Assert.Equal(2.474310329303e-13, p, 1e-19);
    }

    /// <summary>
    /// The 95% CI on a realistic 8-score sample matches scipy.stats.t.interval exactly.
    /// </summary>
    [Fact]
    public void ConfidenceInterval_Matches_Scipy_On_Eight_Scores()
    {
        double[] scores = [0.90, 0.85, 0.60, 0.95, 0.70, 0.88, 0.79, 0.92];

        StatisticalMetrics.ConfidenceInterval? ci = StatisticalMetrics.MeanConfidenceInterval(scores);

        Assert.NotNull(ci);
        Assert.Equal(0.823750000000, ci.Mean, Tolerance);
        Assert.Equal(0.120349194312, ci.StdDev, Tolerance);
        Assert.Equal(0.723135555654, ci.Lower, Tolerance);
        Assert.Equal(0.924364444346, ci.Upper, Tolerance);
        Assert.Equal(8, ci.SampleCount);
    }

    /// <summary>
    /// Small-n honesty: three spread scores give an interval that escapes [0, 1] entirely —
    /// the CI reports what the data supports, not what the score scale suggests. Values from
    /// scipy.stats.t.interval(0.95, 2, ...).
    /// </summary>
    [Fact]
    public void ConfidenceInterval_On_Three_Spread_Scores_Is_Honestly_Wide()
    {
        StatisticalMetrics.ConfidenceInterval? ci =
            StatisticalMetrics.MeanConfidenceInterval([0.0, 0.5, 1.0]);

        Assert.NotNull(ci);
        Assert.Equal(-0.742068855875, ci.Lower, Tolerance);
        Assert.Equal(1.742068855875, ci.Upper, Tolerance);
    }

    /// <summary>
    /// n=2 uses df=1, whose 97.5% point is 12.7 — the interval is enormous, as it must be.
    /// scipy: (-3.311861420852, 4.311861420852).
    /// </summary>
    [Fact]
    public void ConfidenceInterval_With_Two_Values_Uses_Df_One()
    {
        StatisticalMetrics.ConfidenceInterval? ci =
            StatisticalMetrics.MeanConfidenceInterval([0.2, 0.8]);

        Assert.NotNull(ci);
        Assert.Equal(-3.311861420852, ci.Lower, Tolerance);
        Assert.Equal(4.311861420852, ci.Upper, Tolerance);
    }

    /// <summary>
    /// One value has no interval, and neither does an empty sample — null, never a
    /// zero-width fabrication.
    /// </summary>
    [Fact]
    public void ConfidenceInterval_Below_Two_Values_Is_Null()
    {
        Assert.Null(StatisticalMetrics.MeanConfidenceInterval([0.7]));
        Assert.Null(StatisticalMetrics.MeanConfidenceInterval([]));
    }

    /// <summary>
    /// Interval invariants over an arbitrary sample: the interval contains and is symmetric
    /// about the mean, and a higher confidence level widens it.
    /// </summary>
    [Fact]
    public void ConfidenceInterval_Invariants_Hold()
    {
        double[] values = [0.31, 0.62, 0.55, 0.44, 0.71, 0.29, 0.68];

        StatisticalMetrics.ConfidenceInterval? ci95 = StatisticalMetrics.MeanConfidenceInterval(values);
        StatisticalMetrics.ConfidenceInterval? ci99 =
            StatisticalMetrics.MeanConfidenceInterval(values, confidence: 0.99);

        Assert.NotNull(ci95);
        Assert.NotNull(ci99);
        Assert.True(ci95.Lower < ci95.Mean && ci95.Mean < ci95.Upper);
        Assert.Equal(ci95.Mean - ci95.Lower, ci95.Upper - ci95.Mean, 1e-12);
        Assert.True(ci99.Lower < ci95.Lower && ci99.Upper > ci95.Upper);
    }

    /// <summary>
    /// The paired comparison reproduces scipy.stats.ttest_rel and the scipy CI of the mean
    /// difference on an 8-item pairing that hovers just above p = 0.05.
    /// </summary>
    [Fact]
    public void PairedComparison_Matches_Scipy_Ttest_Rel()
    {
        double[] modelA = [0.90, 0.85, 0.60, 0.95, 0.70, 0.88, 0.79, 0.92];
        double[] modelB = [0.82, 0.80, 0.65, 0.90, 0.60, 0.85, 0.81, 0.86];

        StatisticalMetrics.PairedComparisonResult? cmp =
            StatisticalMetrics.PairedComparison(modelA, modelB);

        Assert.NotNull(cmp);
        Assert.Equal(8, cmp.PairCount);
        Assert.Equal(0.037500000000, cmp.MeanDifference, Tolerance);
        Assert.NotNull(cmp.TStatistic);
        Assert.NotNull(cmp.PValue);
        Assert.Equal(2.118296364341, cmp.TStatistic.Value, Tolerance);
        Assert.Equal(0.071902154197, cmp.PValue.Value, Tolerance);
        Assert.Equal(-0.004360719268, cmp.Lower, Tolerance);
        Assert.Equal(0.079360719268, cmp.Upper, Tolerance);
    }

    /// <summary>
    /// A clearly insignificant pairing: p far above 0.05, CI straddling zero. scipy:
    /// t=0.397359707120, p=0.717685644211, CI (-0.052567356760, 0.067567356760).
    /// </summary>
    [Fact]
    public void PairedComparison_Reports_Insignificance_Honestly()
    {
        StatisticalMetrics.PairedComparisonResult? cmp = StatisticalMetrics.PairedComparison(
            [0.5, 0.7, 0.6, 0.55], [0.52, 0.66, 0.63, 0.51]);

        Assert.NotNull(cmp);
        Assert.NotNull(cmp.PValue);
        Assert.Equal(0.717685644211, cmp.PValue.Value, Tolerance);
        Assert.True(cmp.Lower < 0 && cmp.Upper > 0);
    }

    /// <summary>
    /// Zero variance in the differences leaves the t statistic undefined; we report null
    /// (scipy prints a catastrophic-cancellation warning and returns inf/0.0 here — a limit
    /// claim we deliberately refuse: three identical differences are not infinite evidence).
    /// The mean difference and its collapsed interval still report.
    /// </summary>
    [Fact]
    public void PairedComparison_With_Constant_Differences_Reports_Undefined_Not_Zero()
    {
        StatisticalMetrics.PairedComparisonResult? cmp = StatisticalMetrics.PairedComparison(
            [0.5, 0.6, 0.7], [0.4, 0.5, 0.6]);

        Assert.NotNull(cmp);
        Assert.Equal(0.1, cmp.MeanDifference, Tolerance);
        Assert.Equal(cmp.MeanDifference, cmp.Lower, Tolerance);
        Assert.Equal(cmp.MeanDifference, cmp.Upper, Tolerance);
        Assert.Null(cmp.TStatistic);
        Assert.Null(cmp.PValue);
    }

    /// <summary>
    /// Identical samples: mean difference exactly zero with an undefined statistic.
    /// </summary>
    [Fact]
    public void PairedComparison_Of_Identical_Samples_Has_Zero_Difference()
    {
        double[] same = [0.3, 0.9, 0.5];

        StatisticalMetrics.PairedComparisonResult? cmp =
            StatisticalMetrics.PairedComparison(same, same);

        Assert.NotNull(cmp);
        Assert.Equal(0, cmp.MeanDifference);
        Assert.Null(cmp.PValue);
    }

    /// <summary>
    /// Misaligned inputs are a programming error, not a statistics question.
    /// </summary>
    [Fact]
    public void PairedComparison_Rejects_Misaligned_Samples()
    {
        Assert.Throws<ArgumentException>(
            () => StatisticalMetrics.PairedComparison([0.1, 0.2], [0.1]));
    }

    /// <summary>
    /// Fewer than two pairs: null, never a fabricated verdict.
    /// </summary>
    [Fact]
    public void PairedComparison_Below_Two_Pairs_Is_Null()
    {
        Assert.Null(StatisticalMetrics.PairedComparison([0.5], [0.4]));
        Assert.Null(StatisticalMetrics.PairedComparison([], []));
    }

    /// <summary>
    /// Antisymmetry: swapping the sides negates the mean difference and t statistic and
    /// mirrors the interval, while the p-value is unchanged.
    /// </summary>
    [Fact]
    public void PairedComparison_Is_Antisymmetric()
    {
        double[] a = [0.90, 0.85, 0.60, 0.95, 0.70];
        double[] b = [0.82, 0.80, 0.65, 0.90, 0.60];

        StatisticalMetrics.PairedComparisonResult? ab = StatisticalMetrics.PairedComparison(a, b);
        StatisticalMetrics.PairedComparisonResult? ba = StatisticalMetrics.PairedComparison(b, a);

        Assert.NotNull(ab);
        Assert.NotNull(ba);
        Assert.Equal(ab.MeanDifference, -ba.MeanDifference, 1e-12);
        Assert.Equal(ab.TStatistic!.Value, -ba.TStatistic!.Value, 1e-12);
        Assert.Equal(ab.PValue!.Value, ba.PValue!.Value, 1e-12);
        Assert.Equal(ab.Lower, -ba.Upper, 1e-12);
        Assert.Equal(ab.Upper, -ba.Lower, 1e-12);
    }
}

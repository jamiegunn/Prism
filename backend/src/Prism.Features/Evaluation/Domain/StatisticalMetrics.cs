namespace Prism.Features.Evaluation.Domain;

/// <summary>
/// Student-t confidence intervals and paired comparisons for evaluation scores —
/// deterministic implementations differential-tested against scipy.stats
/// (<c>t.ppf</c>, <c>t.cdf</c>, <c>t.interval</c>, <c>ttest_rel</c>).
/// </summary>
/// <remarks>
/// A mean over a handful of items is a point estimate wearing a costume; these methods put
/// the uncertainty back. Everything here is closed-form or a deterministic special-function
/// evaluation — no resampling — so the same data always yields the same interval.
/// </remarks>
public static class StatisticalMetrics
{
    /// <summary>
    /// A Student-t confidence interval on a mean.
    /// </summary>
    /// <param name="Mean">The sample mean.</param>
    /// <param name="Lower">The lower bound of the interval.</param>
    /// <param name="Upper">The upper bound of the interval.</param>
    /// <param name="StdDev">The sample standard deviation (Bessel-corrected, n−1).</param>
    /// <param name="SampleCount">How many values the interval is computed over.</param>
    public sealed record ConfidenceInterval(
        double Mean,
        double Lower,
        double Upper,
        double StdDev,
        int SampleCount);

    /// <summary>
    /// The result of a paired two-sided Student-t comparison between two models' per-item
    /// scores.
    /// </summary>
    /// <param name="PairCount">How many items both sides scored.</param>
    /// <param name="MeanDifference">Mean of the per-item differences (a − b).</param>
    /// <param name="Lower">Lower bound of the confidence interval on the mean difference.</param>
    /// <param name="Upper">Upper bound of the confidence interval on the mean difference.</param>
    /// <param name="TStatistic">The paired t statistic, or null when every pair differs by
    /// exactly the same amount (zero variance — the statistic is undefined, not zero).</param>
    /// <param name="PValue">Two-sided p-value, or null when the statistic is undefined.</param>
    public sealed record PairedComparisonResult(
        int PairCount,
        double MeanDifference,
        double Lower,
        double Upper,
        double? TStatistic,
        double? PValue);

    /// <summary>
    /// Computes a Student-t confidence interval on the mean of <paramref name="values"/>.
    /// Returns null for fewer than two values: one number has a mean but no measurable
    /// uncertainty, and pretending otherwise is the failure mode this type exists to stop.
    /// </summary>
    /// <param name="values">The sample.</param>
    /// <param name="confidence">The confidence level, defaulting to 0.95.</param>
    /// <returns>The interval, or null when it cannot be computed.</returns>
    public static ConfidenceInterval? MeanConfidenceInterval(
        IReadOnlyList<double> values, double confidence = 0.95)
    {
        if (values.Count < 2)
        {
            return null;
        }

        int n = values.Count;
        double mean = values.Average();
        double variance = values.Sum(v => (v - mean) * (v - mean)) / (n - 1);
        double stdDev = Math.Sqrt(variance);
        double standardError = stdDev / Math.Sqrt(n);
        double critical = StudentTQuantile((1 + confidence) / 2.0, n - 1);

        return new ConfidenceInterval(
            mean,
            mean - critical * standardError,
            mean + critical * standardError,
            stdDev,
            n);
    }

    /// <summary>
    /// Runs a paired two-sided Student-t comparison of <paramref name="a"/> against
    /// <paramref name="b"/>, item by item. The inputs must be aligned: element i of each list
    /// scores the same underlying item. Returns null for fewer than two pairs. When every
    /// pair differs by exactly the same amount the interval collapses to a point and the
    /// t statistic and p-value are null — undefined is not the same claim as zero.
    /// </summary>
    /// <param name="a">Per-item scores of the first model.</param>
    /// <param name="b">Per-item scores of the second model, aligned with the first.</param>
    /// <param name="confidence">The confidence level for the interval, defaulting to 0.95.</param>
    /// <returns>The comparison, or null when it cannot be computed.</returns>
    /// <exception cref="ArgumentException">The lists have different lengths.</exception>
    public static PairedComparisonResult? PairedComparison(
        IReadOnlyList<double> a, IReadOnlyList<double> b, double confidence = 0.95)
    {
        if (a.Count != b.Count)
        {
            throw new ArgumentException(
                $"Paired samples must align: got {a.Count} and {b.Count} values.");
        }

        if (a.Count < 2)
        {
            return null;
        }

        int n = a.Count;
        double[] differences = new double[n];
        for (int i = 0; i < n; i++)
        {
            differences[i] = a[i] - b[i];
        }

        double meanDiff = differences.Average();
        double variance = differences.Sum(d => (d - meanDiff) * (d - meanDiff)) / (n - 1);

        if (variance == 0)
        {
            return new PairedComparisonResult(n, meanDiff, meanDiff, meanDiff, null, null);
        }

        double standardError = Math.Sqrt(variance / n);
        double critical = StudentTQuantile((1 + confidence) / 2.0, n - 1);
        double t = meanDiff / standardError;

        // The survival function directly, not 2*(1 - CDF): near-certain CDFs cancel
        // catastrophically and would corrupt small p-values — the ones that matter most.
        double pValue = 2.0 * StudentTSurvival(Math.Abs(t), n - 1);

        return new PairedComparisonResult(
            n,
            meanDiff,
            meanDiff - critical * standardError,
            meanDiff + critical * standardError,
            t,
            pValue);
    }

    /// <summary>
    /// The cumulative distribution function of Student's t with <paramref name="df"/> degrees
    /// of freedom, via the regularized incomplete beta function:
    /// for t ≥ 0, P(T ≤ t) = 1 − I_x(ν/2, 1/2)/2 with x = ν/(ν + t²).
    /// </summary>
    /// <param name="t">The point to evaluate at.</param>
    /// <param name="df">Degrees of freedom.</param>
    /// <returns>P(T ≤ t).</returns>
    internal static double StudentTCdf(double t, double df)
    {
        double tail = StudentTSurvival(Math.Abs(t), df);
        return t >= 0 ? 1.0 - tail : tail;
    }

    /// <summary>
    /// The survival function P(T &gt; t) of Student's t for t ≥ 0, computed directly so tiny
    /// tail probabilities keep their precision (computing 1 − CDF instead would cancel to
    /// only a few significant digits exactly where p-values are smallest).
    /// </summary>
    /// <param name="t">The point to evaluate at; must be non-negative.</param>
    /// <param name="df">Degrees of freedom.</param>
    /// <returns>P(T &gt; t).</returns>
    internal static double StudentTSurvival(double t, double df)
    {
        double x = df / (df + t * t);
        return 0.5 * RegularizedIncompleteBeta(df / 2.0, 0.5, x);
    }

    /// <summary>
    /// The quantile (inverse CDF) of Student's t with <paramref name="df"/> degrees of
    /// freedom, found by bisection on <see cref="StudentTCdf"/> — deterministic, and accurate
    /// far beyond what an interval display needs.
    /// </summary>
    /// <param name="p">The probability, in (0, 1).</param>
    /// <param name="df">Degrees of freedom.</param>
    /// <returns>The value t with P(T ≤ t) = <paramref name="p"/>.</returns>
    internal static double StudentTQuantile(double p, double df)
    {
        if (p is <= 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(p), p, "Probability must be in (0, 1).");
        }

        if (Math.Abs(p - 0.5) < 1e-15)
        {
            return 0;
        }

        // The t quantile is symmetric; solve for the upper half and mirror.
        bool upper = p > 0.5;
        double target = upper ? p : 1 - p;

        double lo = 0;
        double hi = 1;
        while (StudentTCdf(hi, df) < target)
        {
            hi *= 2;
            if (hi > 1e10)
            {
                break;
            }
        }

        for (int i = 0; i < 200; i++)
        {
            double mid = (lo + hi) / 2;
            if (StudentTCdf(mid, df) < target)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        double q = (lo + hi) / 2;
        return upper ? q : -q;
    }

    /// <summary>
    /// The regularized incomplete beta function I_x(a, b), by the standard continued-fraction
    /// expansion (Lentz's method, as in Numerical Recipes' <c>betai</c>/<c>betacf</c>).
    /// </summary>
    /// <param name="a">First shape parameter.</param>
    /// <param name="b">Second shape parameter.</param>
    /// <param name="x">The integration limit, in [0, 1].</param>
    /// <returns>I_x(a, b).</returns>
    internal static double RegularizedIncompleteBeta(double a, double b, double x)
    {
        if (x <= 0)
        {
            return 0;
        }

        if (x >= 1)
        {
            return 1;
        }

        double logBeta = LogGamma(a + b) - LogGamma(a) - LogGamma(b)
            + a * Math.Log(x) + b * Math.Log(1 - x);
        double front = Math.Exp(logBeta);

        // The continued fraction converges fast for x < (a+1)/(a+b+2); use the symmetry
        // I_x(a,b) = 1 - I_{1-x}(b,a) on the other side.
        if (x < (a + 1) / (a + b + 2))
        {
            return front * BetaContinuedFraction(a, b, x) / a;
        }

        return 1.0 - Math.Exp(
            LogGamma(a + b) - LogGamma(a) - LogGamma(b)
            + b * Math.Log(1 - x) + a * Math.Log(x))
            * BetaContinuedFraction(b, a, 1 - x) / b;
    }

    /// <summary>
    /// Evaluates the continued fraction for the incomplete beta function by the modified
    /// Lentz method.
    /// </summary>
    /// <param name="a">First shape parameter.</param>
    /// <param name="b">Second shape parameter.</param>
    /// <param name="x">The integration limit.</param>
    /// <returns>The continued-fraction value.</returns>
    private static double BetaContinuedFraction(double a, double b, double x)
    {
        const double Tiny = 1e-300;
        const double Epsilon = 1e-15;

        double qab = a + b;
        double qap = a + 1;
        double qam = a - 1;
        double c = 1.0;
        double d = 1.0 - qab * x / qap;
        if (Math.Abs(d) < Tiny)
        {
            d = Tiny;
        }

        d = 1.0 / d;
        double h = d;

        for (int m = 1; m <= 300; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1.0 + aa * d;
            if (Math.Abs(d) < Tiny)
            {
                d = Tiny;
            }

            c = 1.0 + aa / c;
            if (Math.Abs(c) < Tiny)
            {
                c = Tiny;
            }

            d = 1.0 / d;
            h *= d * c;

            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1.0 + aa * d;
            if (Math.Abs(d) < Tiny)
            {
                d = Tiny;
            }

            c = 1.0 + aa / c;
            if (Math.Abs(c) < Tiny)
            {
                c = Tiny;
            }

            d = 1.0 / d;
            double del = d * c;
            h *= del;

            if (Math.Abs(del - 1.0) < Epsilon)
            {
                break;
            }
        }

        return h;
    }

    /// <summary>
    /// The natural log of the gamma function (Lanczos approximation, g = 7, n = 9 — the
    /// coefficients in wide circulation from Numerical Recipes lineage).
    /// </summary>
    /// <param name="x">The argument, positive.</param>
    /// <returns>ln Γ(x).</returns>
    private static double LogGamma(double x)
    {
        double[] coefficients =
        [
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7,
        ];

        if (x < 0.5)
        {
            // Reflection formula.
            return Math.Log(Math.PI / Math.Sin(Math.PI * x)) - LogGamma(1 - x);
        }

        x -= 1;
        double sum = 0.99999999999980993;
        for (int i = 0; i < coefficients.Length; i++)
        {
            sum += coefficients[i] / (x + i + 1);
        }

        double t = x + coefficients.Length - 0.5;
        return 0.5 * Math.Log(2 * Math.PI) + (x + 0.5) * Math.Log(t) - t + Math.Log(sum);
    }
}

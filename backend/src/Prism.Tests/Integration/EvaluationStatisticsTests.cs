using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Application.GetEvaluationResults;
using Prism.Features.Evaluation.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Proofs that the evaluation summary carries honest uncertainty: Student-t confidence
/// intervals on per-model means and paired comparisons between models, paired by dataset
/// item — with absences (one item, no shared items, failed calls) staying absent.
/// </summary>
[Collection("Database")]
public sealed class EvaluationStatisticsTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationStatisticsTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public EvaluationStatisticsTests(DatabaseFixture fixture) => _fixture = fixture;

    private static EvaluationResult ResultRow(
        Guid evaluationId,
        string model,
        Guid recordId,
        double? accuracy,
        string? error = null) => new()
        {
            EvaluationId = evaluationId,
            Model = model,
            RecordId = recordId,
            Input = "in",
            ExpectedOutput = "exp",
            ActualOutput = error is null ? "act" : null,
            Scores = accuracy is null
            ? []
            : new Dictionary<string, double> { ["accuracy"] = accuracy.Value },
            Error = error,
            LatencyMs = 5,
        };

    /// <summary>
    /// The summary reports a scipy-exact 95% CI per model and a scipy-exact paired
    /// comparison, paired by dataset record — including only the items both models scored.
    /// The reference numbers are scipy.stats.t.interval / ttest_rel on the same values.
    /// </summary>
    [Fact]
    public async Task Summary_Reports_Scipy_Exact_Intervals_And_Paired_Comparison()
    {
        double[] scoresA = [0.90, 0.85, 0.60, 0.95, 0.70, 0.88, 0.79, 0.92];
        double[] scoresB = [0.82, 0.80, 0.65, 0.90, 0.60, 0.85, 0.81, 0.86];
        Guid[] records = [.. Enumerable.Range(0, 8).Select(_ => Guid.NewGuid())];

        await using AppDbContext db = _fixture.CreateContext();
        var evaluation = new EvaluationEntity
        {
            DatasetId = Guid.NewGuid(),
            Name = "stats-proof",
            Models = ["model-a", "model-b"],
            ScoringMethods = ["exact_match"],
            Status = EvaluationStatus.Completed,
        };
        db.Add(evaluation);
        for (int i = 0; i < 8; i++)
        {
            db.Add(ResultRow(evaluation.Id, "model-a", records[i], scoresA[i]));
            db.Add(ResultRow(evaluation.Id, "model-b", records[i], scoresB[i]));
        }

        // An item only model A scored must not enter the pairing — and model B's FAILED
        // attempt at that same item (which pathologically carries a score) must not smuggle
        // it in: failed calls are excluded from pairing and intervals alike.
        Guid extraRecord = Guid.NewGuid();
        db.Add(ResultRow(evaluation.Id, "model-a", extraRecord, 0.05));
        db.Add(ResultRow(evaluation.Id, "model-b", extraRecord, 0.0, error: "boom"));
        await db.SaveChangesAsync();

        var handler = new GetEvaluationResultsHandler(_fixture.CreateContext());
        Result<EvaluationSummaryDto> result = await handler.HandleAsync(
            new GetEvaluationResultsQuery(evaluation.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // Model B's interval covers exactly the 8 paired scores: scipy t.interval gives
        // (0.698233518167, 0.874266481833) for mean 0.78625.
        ModelSummaryDto modelB = Assert.Single(
            result.Value.ModelSummaries, m => m.Model == "model-b");
        ScoreIntervalDto intervalB = modelB.ScoreIntervals["accuracy"];
        Assert.Equal(8, intervalB.SampleCount);
        Assert.Equal(0.78625, intervalB.Mean, 1e-9);
        Assert.Equal(0.698233518167, intervalB.Lower, 1e-9);
        Assert.Equal(0.874266481833, intervalB.Upper, 1e-9);

        // Model A has 9 scored items (the extra unpaired one included) — its interval is
        // over all 9, but the comparison pairs only the 8 shared records.
        ModelSummaryDto modelA = Assert.Single(
            result.Value.ModelSummaries, m => m.Model == "model-a");
        Assert.Equal(9, modelA.ScoreIntervals["accuracy"].SampleCount);

        ModelComparisonDto cmp = Assert.Single(result.Value.Comparisons);
        Assert.Equal("accuracy", cmp.Metric);
        Assert.Equal("model-a", cmp.ModelA);
        Assert.Equal("model-b", cmp.ModelB);
        Assert.Equal(8, cmp.PairCount);
        Assert.Equal(0.0375, cmp.MeanDifference, 1e-9);
        Assert.NotNull(cmp.TStatistic);
        Assert.NotNull(cmp.PValue);
        Assert.Equal(2.118296364341, cmp.TStatistic.Value, 1e-9);
        Assert.Equal(0.071902154197, cmp.PValue.Value, 1e-9);
        Assert.Equal(-0.004360719268, cmp.Lower, 1e-9);
        Assert.Equal(0.079360719268, cmp.Upper, 1e-9);

        // The definitions make the numbers citable.
        Assert.Contains("scipy.stats.t.interval", result.Value.ScoreDefinitions["ci95"]);
        Assert.Contains("ttest_rel", result.Value.ScoreDefinitions["paired comparison"]);
    }

    /// <summary>
    /// Absences stay absent: a single-model evaluation has no comparisons, and a metric
    /// scored on one item has no interval — while the mean itself still reports.
    /// </summary>
    [Fact]
    public async Task One_Item_Or_One_Model_Yields_No_Fabricated_Statistics()
    {
        await using AppDbContext db = _fixture.CreateContext();
        var evaluation = new EvaluationEntity
        {
            DatasetId = Guid.NewGuid(),
            Name = "stats-absence-proof",
            Models = ["only-model"],
            ScoringMethods = ["exact_match"],
            Status = EvaluationStatus.Completed,
        };
        db.Add(evaluation);
        db.Add(ResultRow(evaluation.Id, "only-model", Guid.NewGuid(), 1.0));
        await db.SaveChangesAsync();

        var handler = new GetEvaluationResultsHandler(_fixture.CreateContext());
        Result<EvaluationSummaryDto> result = await handler.HandleAsync(
            new GetEvaluationResultsQuery(evaluation.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        ModelSummaryDto summary = Assert.Single(result.Value.ModelSummaries);
        Assert.Equal(1.0, summary.AverageScores["accuracy"]);
        Assert.Empty(summary.ScoreIntervals);
        Assert.Empty(result.Value.Comparisons);
        Assert.False(result.Value.ScoreDefinitions.ContainsKey("ci95"));
        Assert.False(result.Value.ScoreDefinitions.ContainsKey("paired comparison"));
    }
}

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Analytics.Application.Dtos;
using Prism.Features.Analytics.Application.GetPerformance;
using Prism.Features.Analytics.Application.GetUsage;
using Prism.Features.Analytics.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers the Analytics aggregations: that the numbers are right, and that they are computed
/// by the database rather than by loading the table.
/// </summary>
/// <remarks>
/// Both handlers previously called <c>ToListAsync()</c> on the whole filtered table and
/// aggregated in memory. That was invisible while <c>UsageLog</c> had no writer; now that
/// Phase 2 populates it, the cost is real.
/// </remarks>
[Collection("Database")]
public sealed class AnalyticsAggregationTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsAggregationTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public AnalyticsAggregationTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Totals and per-model breakdowns must be arithmetically correct.
    /// </summary>
    [Fact]
    public async Task Usage_Totals_And_Breakdowns_Are_Correct()
    {
        string tag = $"usage-{Guid.NewGuid():N}";
        DateTime window = await SeedAsync(tag,
        [
            ("gpt-4", 100, 50, 200, 1.5m),
            ("gpt-4", 200, 100, 300, 3.0m),
            ("llama", 10, 5, 50, null),
        ]);

        var handler = new GetUsageHandler(_fixture.CreateContext());

        Result<UsageSummaryDto> result = await handler.HandleAsync(
            new GetUsageQuery(window.AddMinutes(-5), window.AddMinutes(5), null, tag, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        UsageSummaryDto usage = result.Value;

        Assert.Equal(3, usage.TotalRequests);
        Assert.Equal(310, usage.TotalPromptTokens);
        Assert.Equal(155, usage.TotalCompletionTokens);
        Assert.Equal(465, usage.TotalTokens);
        Assert.Equal(4.5m, usage.TotalCost);

        UsageByModelDto gpt4 = usage.ByModel.Single(m => m.Model == "gpt-4");
        Assert.Equal(2, gpt4.RequestCount);
        Assert.Equal(450, gpt4.TotalTokens);
        Assert.Equal(4.5m, gpt4.TotalCost);

        // The unpriced model must not contribute a fabricated zero to the priced total.
        Assert.Null(usage.ByModel.Single(m => m.Model == "llama").TotalCost);
    }

    /// <summary>
    /// Percentiles must reflect the distribution, not just the mean.
    /// </summary>
    [Fact]
    public async Task Latency_Percentiles_Track_The_Distribution()
    {
        string tag = $"perf-{Guid.NewGuid():N}";

        // 1..100 ms, so p50 and p95 are known and far apart.
        (string Model, int Prompt, int Completion, long Latency, decimal? Cost)[] rows =
            [.. Enumerable.Range(1, 100).Select(i => ("m", 1, 1, (long)i, (decimal?)null))];

        DateTime window = await SeedAsync(tag, rows);

        var handler = new GetPerformanceHandler(_fixture.CreateContext());

        Result<PerformanceSummaryDto> result = await handler.HandleAsync(
            new GetPerformanceQuery(window.AddMinutes(-5), window.AddMinutes(5), "m"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        PerformanceSummaryDto perf = result.Value;

        Assert.InRange(perf.P50LatencyMs, 49, 52);
        Assert.InRange(perf.P95LatencyMs, 94, 97);
        Assert.InRange(perf.AverageLatencyMs, 50, 51);

        // A p95 that equals the median means the percentile is not being computed at all.
        Assert.True(
            perf.P95LatencyMs > perf.P50LatencyMs,
            "p95 did not exceed p50 over a uniform 1-100ms distribution.");

        PerformanceByModelDto model = Assert.Single(perf.ByModel);
        Assert.Equal(100, model.RequestCount);
    }

    /// <summary>
    /// An empty window must return zeros rather than throwing.
    /// </summary>
    [Fact]
    public async Task An_Empty_Window_Returns_Zeros()
    {
        var handler = new GetPerformanceHandler(_fixture.CreateContext());

        Result<PerformanceSummaryDto> result = await handler.HandleAsync(
            new GetPerformanceQuery(
                new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(1990, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.P95LatencyMs);
        Assert.Empty(result.Value.ByModel);
    }

    /// <summary>
    /// Aggregation must stay in the database as the log grows. This is the property the
    /// rewrite exists for, and the only way to check it is to actually put rows in.
    /// </summary>
    [Fact]
    public async Task Aggregation_Stays_Fast_At_Volume()
    {
        const int rowCount = 50_000;
        string tag = $"volume-{Guid.NewGuid():N}";

        DateTime window = await SeedBulkAsync(tag, rowCount);

        var usage = new GetUsageHandler(_fixture.CreateContext());
        var perf = new GetPerformanceHandler(_fixture.CreateContext());

        var stopwatch = Stopwatch.StartNew();

        Result<UsageSummaryDto> usageResult = await usage.HandleAsync(
            new GetUsageQuery(window.AddHours(-1), window.AddHours(1), null, tag, null),
            CancellationToken.None);

        Result<PerformanceSummaryDto> perfResult = await perf.HandleAsync(
            new GetPerformanceQuery(window.AddHours(-1), window.AddHours(1), null),
            CancellationToken.None);

        stopwatch.Stop();

        Assert.True(usageResult.IsSuccess);
        Assert.True(perfResult.IsSuccess);
        Assert.Equal(rowCount, usageResult.Value.TotalRequests);

        // Deliberately loose: this is a smoke alarm for "someone reintroduced ToListAsync over
        // the whole table", not a benchmark. The in-memory version materialised 50k entities
        // per call and was far slower than this bound.
        Assert.True(
            stopwatch.ElapsedMilliseconds < 5_000,
            $"Aggregating {rowCount} rows took {stopwatch.ElapsedMilliseconds}ms. " +
            "That suggests the work moved back out of the database.");
    }

    private async Task<DateTime> SeedAsync(
        string tag, (string Model, int Prompt, int Completion, long Latency, decimal? Cost)[] rows)
    {
        await using AppDbContext db = _fixture.CreateContext();
        DateTime now = DateTime.UtcNow;

        foreach ((string model, int prompt, int completion, long latency, decimal? cost) in rows)
        {
            db.Set<UsageLog>().Add(new UsageLog
            {
                Model = model,
                PromptTokens = prompt,
                CompletionTokens = completion,
                LatencyMs = latency,
                SourceModule = tag,
                Cost = cost,
            });
        }

        await db.SaveChangesAsync();
        return now;
    }

    private async Task<DateTime> SeedBulkAsync(string tag, int rowCount)
    {
        await using AppDbContext db = _fixture.CreateContext();
        DateTime now = DateTime.UtcNow;

        for (int i = 0; i < rowCount; i++)
        {
            db.Set<UsageLog>().Add(new UsageLog
            {
                Model = i % 3 == 0 ? "model-a" : "model-b",
                PromptTokens = 10,
                CompletionTokens = 20,
                LatencyMs = i % 500,
                SourceModule = tag,
            });
        }

        await db.SaveChangesAsync();
        return now;
    }
}

using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Analytics.Application.Dtos;
using Prism.Features.Analytics.Application.GetPerformance;
using Prism.Features.Analytics.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers the isolation properties the rest of the integration suite depends on.
/// </summary>
/// <remarks>
/// <para>
/// These exist because of a real failure, not a hypothetical one. The suite can run against a
/// throwaway Testcontainers database or against a long-lived one named by <c>PRISM_TEST_DB</c>.
/// The first is empty every run; the second was not, and nothing emptied it. An analytics test
/// seeded a hundred rows under the fixed model name <c>"m"</c> and asserted it could see a
/// hundred — true on a fresh database, false on the second run of the day, when it saw two
/// hundred.
/// </para>
/// <para>
/// That combination is the worst possible one: green in CI, red on the machine of whoever runs
/// the suite twice, and looking for all the world like flakiness. Introducing a pre-commit hook
/// turned it from a curiosity into a blocker, because the hook runs the suite on every commit.
/// </para>
/// </remarks>
[Collection("Database")]
public sealed class TestDatabaseIsolationTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestDatabaseIsolationTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public TestDatabaseIsolationTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Seeding the same scenario twice inside one time window must not double the counts.
    /// </summary>
    /// <remarks>
    /// This reproduces the original failure inside a single test run. Two identical batches are
    /// written the way two consecutive suite runs would write them; a per-run model name has to
    /// keep them apart, because the performance query has no other discriminator to offer.
    /// </remarks>
    [Fact]
    public async Task Two_Runs_Of_The_Same_Scenario_Do_Not_Contaminate_Each_Other()
    {
        DateTime window = DateTime.UtcNow;

        string firstModel = await SeedLatencyRowsAsync();
        string secondModel = await SeedLatencyRowsAsync();

        Assert.NotEqual(firstModel, secondModel);

        foreach (string model in new[] { firstModel, secondModel })
        {
            var handler = new GetPerformanceHandler(_fixture.CreateContext());

            Result<PerformanceSummaryDto> result = await handler.HandleAsync(
                new GetPerformanceQuery(window.AddMinutes(-5), window.AddMinutes(5), model),
                CancellationToken.None);

            Assert.True(result.IsSuccess);

            PerformanceByModelDto row = Assert.Single(result.Value.ByModel);
            Assert.Equal(
                100,
                row.RequestCount);
        }
    }

    /// <summary>
    /// A shared time window is not isolation, and this pins why.
    /// </summary>
    /// <remarks>
    /// Written as a positive assertion about the contaminated case rather than a comment, so
    /// that anyone tempted to reinstate a fixed model name sees the count they would get.
    /// </remarks>
    [Fact]
    public async Task A_Fixed_Model_Name_Would_See_Every_Runs_Rows()
    {
        DateTime window = DateTime.UtcNow;
        string shared = $"shared-{Guid.NewGuid():N}";

        await SeedLatencyRowsAsync(shared);
        await SeedLatencyRowsAsync(shared);

        var handler = new GetPerformanceHandler(_fixture.CreateContext());

        Result<PerformanceSummaryDto> result = await handler.HandleAsync(
            new GetPerformanceQuery(window.AddMinutes(-5), window.AddMinutes(5), shared),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        PerformanceByModelDto row = Assert.Single(result.Value.ByModel);
        Assert.Equal(200, row.RequestCount);
    }

    /// <summary>
    /// The fixture must refuse to empty the database the application itself runs on.
    /// </summary>
    [Theory]
    [InlineData("Host=localhost;Port=5438;Database=prism;Username=postgres;Password=postgres")]
    [InlineData("Host=localhost;Port=5438;Database=PRISM;Username=postgres;Password=postgres")]
    public void Pointing_The_Suite_At_The_Application_Database_Is_Refused(string connectionString)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => DatabaseFixture.GuardAgainstApplicationDatabase(connectionString));

        // The message has to name the variable and suggest a way out, because this fires at
        // startup before any test has run and is the only thing the developer will see.
        Assert.Contains("PRISM_TEST_DB", error.Message, StringComparison.Ordinal);
        Assert.Contains("prism_test", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A separate database is accepted.
    /// </summary>
    [Theory]
    [InlineData("Host=localhost;Port=5438;Database=prism_test;Username=postgres;Password=postgres")]
    [InlineData("Host=localhost;Port=5438;Database=prism_gate;Username=postgres;Password=postgres")]
    public void A_Database_Other_Than_The_Applications_Is_Allowed(string connectionString) =>
        DatabaseFixture.GuardAgainstApplicationDatabase(connectionString);

    private async Task<string> SeedLatencyRowsAsync(string? model = null)
    {
        model ??= $"isolation-{Guid.NewGuid():N}";

        await using AppDbContext db = _fixture.CreateContext();

        foreach (int latency in Enumerable.Range(1, 100))
        {
            db.Set<UsageLog>().Add(new UsageLog
            {
                Model = model,
                PromptTokens = 1,
                CompletionTokens = 1,
                LatencyMs = latency,
                SourceModule = "isolation-check",
            });
        }

        await db.SaveChangesAsync();
        return model;
    }
}

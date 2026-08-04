using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Jobs;
using Prism.Common.Results;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers the properties a job queue has to hold under failure: a job runs once even with
/// several workers competing, and a job survives the death of the worker holding it.
/// </summary>
/// <remarks>
/// Tested against a real PostgreSQL instance rather than a fake, because the guarantees come
/// from <c>FOR UPDATE SKIP LOCKED</c> and row-level locking. A fake store would assert only
/// that the C# around the database is shaped correctly, which is not where the risk is.
/// </remarks>
[Collection("Database")]
public sealed class JobLeaseTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobLeaseTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public JobLeaseTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Two workers racing for one job must not both get it. This is the property that
    /// separates a queue from a list.
    /// </summary>
    [Fact]
    public async Task Concurrent_Workers_Never_Claim_The_Same_Job()
    {
        const int jobCount = 25;
        const int workerCount = 6;

        string jobType = $"race-{Guid.NewGuid():N}";
        await SeedQueuedJobsAsync(jobType, jobCount);

        // Every worker drains the queue as fast as it can, all against the same database.
        IEnumerable<Task<List<Guid>>> workers = Enumerable.Range(0, workerCount)
            .Select(async _ =>
            {
                List<Guid> mine = [];
                await using AppDbContext db = _fixture.CreateContext();
                IJobLeaseStore store = CreateStore(db);

                while (await store.ClaimNextAsync([jobType], CancellationToken.None) is { } job)
                {
                    mine.Add(job.Id);
                }

                return mine;
            });

        List<Guid>[] claimsPerWorker = await Task.WhenAll(workers);
        List<Guid> allClaims = claimsPerWorker.SelectMany(c => c).ToList();

        Assert.Equal(jobCount, allClaims.Count);
        Assert.Equal(jobCount, allClaims.Distinct().Count());
    }

    /// <summary>
    /// A job whose worker died — no heartbeat past the lease — must return to the queue rather
    /// than sit marked running with nobody working on it.
    /// </summary>
    [Fact]
    public async Task A_Job_Whose_Worker_Died_Returns_To_The_Queue()
    {
        string jobType = $"abandoned-{Guid.NewGuid():N}";
        await SeedQueuedJobsAsync(jobType, 1);

        MutableClock clock = new MutableClock(DateTimeOffset.UtcNow);

        await using AppDbContext db = _fixture.CreateContext();
        IJobLeaseStore store = CreateStore(db, clock);

        DurableJob? claimed = await store.ClaimNextAsync([jobType], CancellationToken.None);
        Assert.NotNull(claimed);

        // The worker dies here: no further heartbeats, no completion.
        clock.Advance(TimeSpan.FromMinutes(10));

        // Recovery is global by design, so other tests' abandoned jobs may be swept up in the
        // same pass. Assert on this job's fate rather than on a shared count.
        int reclaimed = await store.ReclaimExpiredAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.True(reclaimed >= 1, "The abandoned job was not recovered.");

        await using AppDbContext verify = _fixture.CreateContext();
        DurableJob after = await verify.Set<DurableJob>().AsNoTracking()
            .FirstAsync(j => j.Id == claimed!.Id);

        Assert.Equal(JobStatus.Queued, after.Status);
        Assert.Equal(1, after.RetryCount);

        // And it must be claimable again — recovery that leaves a job unclaimable is not recovery.
        DurableJob? reclaimedJob = await CreateStore(verify, clock)
            .ClaimNextAsync([jobType], CancellationToken.None);
        Assert.NotNull(reclaimedJob);
    }

    /// <summary>
    /// A job that keeps being abandoned must eventually fail. Without this it is reclaimed
    /// forever, occupying a worker on every pass and starving everything behind it.
    /// </summary>
    [Fact]
    public async Task A_Job_That_Exhausts_Its_Retries_Fails_Rather_Than_Looping()
    {
        string jobType = $"poison-{Guid.NewGuid():N}";
        await SeedQueuedJobsAsync(jobType, 1, maxRetries: 2);

        MutableClock clock = new MutableClock(DateTimeOffset.UtcNow);

        await using AppDbContext db = _fixture.CreateContext();
        IJobLeaseStore store = CreateStore(db, clock);

        Guid jobId = Guid.Empty;

        // Claim and abandon repeatedly: two retries, then the third abandonment is terminal.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            DurableJob? claimed = await store.ClaimNextAsync([jobType], CancellationToken.None);
            Assert.NotNull(claimed);
            jobId = claimed!.Id;

            clock.Advance(TimeSpan.FromMinutes(10));
            await store.ReclaimExpiredAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        }

        await using AppDbContext verify = _fixture.CreateContext();
        DurableJob after = await verify.Set<DurableJob>().AsNoTracking().FirstAsync(j => j.Id == jobId);

        Assert.Equal(JobStatus.Failed, after.Status);

        DurableJob? shouldBeNothing = await CreateStore(verify, clock)
            .ClaimNextAsync([jobType], CancellationToken.None);
        Assert.Null(shouldBeNothing);
    }

    /// <summary>
    /// A failure inside a job retries until the budget is spent, then fails permanently.
    /// </summary>
    [Fact]
    public async Task A_Failing_Job_Retries_Then_Fails_Permanently()
    {
        string jobType = $"failing-{Guid.NewGuid():N}";
        await SeedQueuedJobsAsync(jobType, 1, maxRetries: 2);

        await using AppDbContext db = _fixture.CreateContext();
        IJobLeaseStore store = CreateStore(db);

        DurableJob? job = await store.ClaimNextAsync([jobType], CancellationToken.None);
        Assert.NotNull(job);

        Result<JobStatus> first = await store.CompleteAsync(job!.Id, "boom", CancellationToken.None);
        Assert.Equal(JobStatus.Queued, first.Value);

        await store.ClaimNextAsync([jobType], CancellationToken.None);
        Result<JobStatus> second = await store.CompleteAsync(job.Id, "boom", CancellationToken.None);
        Assert.Equal(JobStatus.Queued, second.Value);

        await store.ClaimNextAsync([jobType], CancellationToken.None);
        Result<JobStatus> third = await store.CompleteAsync(job.Id, "boom", CancellationToken.None);
        Assert.Equal(JobStatus.Failed, third.Value);
    }

    /// <summary>
    /// A worker only claims job types it can execute; anything else stays queued for a worker
    /// that can.
    /// </summary>
    [Fact]
    public async Task A_Worker_Only_Claims_Types_It_Handles()
    {
        string mine = $"mine-{Guid.NewGuid():N}";
        string theirs = $"theirs-{Guid.NewGuid():N}";
        await SeedQueuedJobsAsync(theirs, 1);

        await using AppDbContext db = _fixture.CreateContext();
        IJobLeaseStore store = CreateStore(db);

        Assert.Null(await store.ClaimNextAsync([mine], CancellationToken.None));
    }

    private static IJobLeaseStore CreateStore(AppDbContext db, TimeProvider? clock = null)
        => new DbJobLeaseStore(db, NullLogger<DbJobLeaseStore>.Instance, clock);

    private async Task SeedQueuedJobsAsync(string jobType, int count, int maxRetries = 3)
    {
        await using AppDbContext db = _fixture.CreateContext();

        for (int i = 0; i < count; i++)
        {
            db.Set<DurableJob>().Add(new DurableJob
            {
                JobType = jobType,
                Status = JobStatus.Queued,
                TotalItems = 1,
                MaxRetries = maxRetries,
            });
        }

        await db.SaveChangesAsync();
    }
}

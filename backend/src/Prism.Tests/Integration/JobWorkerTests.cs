using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Jobs;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers the worker that drives the job queue: it runs work, records outcomes, keeps its
/// lease alive while working, and treats shutdown differently from failure.
/// </summary>
/// <remarks>
/// Absence of this worker is why Evaluation, Batch, Fine-Tuning and RAG ingestion all created
/// rows that nothing ever executed.
/// </remarks>
[Collection("Database")]
public sealed class JobWorkerTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobWorkerTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public JobWorkerTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// The base case that did not exist before: a queued job actually runs.
    /// </summary>
    [Fact]
    public async Task A_Queued_Job_Runs_And_Is_Marked_Complete()
    {
        string jobType = $"work-{Guid.NewGuid():N}";
        Guid jobId = await SeedJobAsync(jobType);

        var handler = new RecordingHandler(jobType);
        JobWorker worker = CreateWorker(handler);

        Guid? ran = await worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(jobId, ran);
        Assert.Equal(1, handler.Executions);
        Assert.Equal(JobStatus.Complete, await StatusOfAsync(jobId));
    }

    /// <summary>
    /// A handler that throws must not lose the job — it goes back to the queue while retries
    /// remain, and fails permanently once they are spent.
    /// </summary>
    [Fact]
    public async Task A_Throwing_Handler_Requeues_Then_Fails_Permanently()
    {
        string jobType = $"boom-{Guid.NewGuid():N}";
        Guid jobId = await SeedJobAsync(jobType, maxRetries: 1);

        var handler = new ThrowingHandler(jobType);
        JobWorker worker = CreateWorker(handler);

        await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(JobStatus.Queued, await StatusOfAsync(jobId));

        await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(JobStatus.Failed, await StatusOfAsync(jobId));

        Assert.Equal(2, handler.Executions);

        // A permanently failed job must not be picked up again.
        Assert.Null(await worker.RunOnceAsync(CancellationToken.None));
    }

    /// <summary>
    /// A job longer than the heartbeat interval must have its lease refreshed while it runs.
    /// Without this the reclaim sweep would hand a healthy running job to a second worker.
    /// </summary>
    /// <remarks>
    /// Samples the heartbeat twice *while the handler is still working*. An earlier version of
    /// this test compared before-run to after-run and passed even with the heartbeat disabled,
    /// because claiming a job stamps the heartbeat once — it proved only that the claim worked.
    /// </remarks>
    [Fact]
    public async Task A_Long_Running_Job_Keeps_Its_Lease_Fresh()
    {
        string jobType = $"slow-{Guid.NewGuid():N}";
        Guid jobId = await SeedJobAsync(jobType);

        var handler = new SlowHandler(jobType, TimeSpan.FromSeconds(3));
        JobWorker worker = CreateWorker(
            handler,
            new JobWorkerOptions { HeartbeatInterval = TimeSpan.FromMilliseconds(100) });

        Task<Guid?> run = worker.RunOnceAsync(CancellationToken.None);

        await Task.Delay(400);
        DateTime? firstSample = await HeartbeatOfAsync(jobId);

        await Task.Delay(800);
        DateTime? secondSample = await HeartbeatOfAsync(jobId);

        await run;

        Assert.NotNull(firstSample);
        Assert.NotNull(secondSample);
        Assert.True(
            secondSample > firstSample,
            $"The lease was not refreshed while the job ran: {firstSample:O} then {secondSample:O}. " +
            "A stalled heartbeat means the reclaim sweep will steal a job that is progressing fine.");

        Assert.Equal(JobStatus.Complete, await StatusOfAsync(jobId));
    }

    /// <summary>
    /// Shutdown is not failure. An interrupted job must keep its retry budget and be left for
    /// the reclaim sweep, otherwise every deployment spends a retry on all in-flight work.
    /// </summary>
    [Fact]
    public async Task Shutdown_Does_Not_Consume_A_Retry()
    {
        string jobType = $"interrupted-{Guid.NewGuid():N}";
        Guid jobId = await SeedJobAsync(jobType);

        var handler = new SlowHandler(jobType, TimeSpan.FromSeconds(30));
        JobWorker worker = CreateWorker(handler);

        using var cts = new CancellationTokenSource();
        Task<Guid?> run = worker.RunOnceAsync(cts.Token);

        await Task.Delay(200);
        await cts.CancelAsync();
        await run;

        await using AppDbContext db = _fixture.CreateContext();
        DurableJob job = await db.Set<DurableJob>().AsNoTracking().FirstAsync(j => j.Id == jobId);

        Assert.Equal(0, job.RetryCount);
        Assert.NotEqual(JobStatus.Failed, job.Status);
    }

    /// <summary>
    /// A worker with no handler for a queued type must leave it alone rather than claiming and
    /// failing it.
    /// </summary>
    [Fact]
    public async Task A_Job_With_No_Handler_Is_Left_Queued()
    {
        string orphanType = $"orphan-{Guid.NewGuid():N}";
        Guid jobId = await SeedJobAsync(orphanType);

        JobWorker worker = CreateWorker(new RecordingHandler($"other-{Guid.NewGuid():N}"));

        Assert.Null(await worker.RunOnceAsync(CancellationToken.None));
        Assert.Equal(JobStatus.Queued, await StatusOfAsync(jobId));
    }

    private JobWorker CreateWorker(IJobHandler handler, JobWorkerOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_fixture);
        services.AddSingleton(handler);
        services.AddScoped(sp => sp.GetRequiredService<DatabaseFixture>().CreateContext());
        services.AddScoped<IJobLeaseStore, DbJobLeaseStore>();
        services.AddScoped<IJobStore, DbJobStore>();

        return new JobWorker(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            options ?? new JobWorkerOptions { HeartbeatInterval = TimeSpan.FromMilliseconds(100) },
            NullLogger<JobWorker>.Instance);
    }

    private async Task<Guid> SeedJobAsync(string jobType, int maxRetries = 3)
    {
        await using AppDbContext db = _fixture.CreateContext();

        var job = new DurableJob
        {
            JobType = jobType,
            Status = JobStatus.Queued,
            TotalItems = 1,
            MaxRetries = maxRetries,
        };

        db.Set<DurableJob>().Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<JobStatus> StatusOfAsync(Guid jobId)
    {
        await using AppDbContext db = _fixture.CreateContext();
        return (await db.Set<DurableJob>().AsNoTracking().FirstAsync(j => j.Id == jobId)).Status;
    }

    private async Task<DateTime?> HeartbeatOfAsync(Guid jobId)
    {
        await using AppDbContext db = _fixture.CreateContext();
        return (await db.Set<DurableJob>().AsNoTracking().FirstAsync(j => j.Id == jobId)).LastHeartbeat;
    }

    private sealed class RecordingHandler : IJobHandler
    {
        public RecordingHandler(string jobType) => JobType = jobType;

        public string JobType { get; }

        public int Executions { get; private set; }

        public Task ExecuteAsync(DurableJob job, CancellationToken ct)
        {
            Executions++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IJobHandler
    {
        public ThrowingHandler(string jobType) => JobType = jobType;

        public string JobType { get; }

        public int Executions { get; private set; }

        public Task ExecuteAsync(DurableJob job, CancellationToken ct)
        {
            Executions++;
            throw new InvalidOperationException("handler exploded");
        }
    }

    private sealed class SlowHandler : IJobHandler
    {
        private readonly TimeSpan _duration;

        public SlowHandler(string jobType, TimeSpan duration)
        {
            JobType = jobType;
            _duration = duration;
        }

        public string JobType { get; }

        public Task ExecuteAsync(DurableJob job, CancellationToken ct) => Task.Delay(_duration, ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Jobs;
using Prism.Common.Results;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.BatchInference.Application.RetryFailed;
using Prism.Features.BatchInference.Application.RunBatch;
using Prism.Features.BatchInference.Application.UpdateBatchJobStatus;
using Prism.Features.BatchInference.Domain;
using Prism.Features.Datasets.Domain;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers batch inference actually running, and the controls over a run doing something.
/// </summary>
/// <remarks>
/// <c>BatchResult</c> had no writer anywhere in the codebase, so results, download and retry
/// all read a permanently empty table. Pause was unreachable: it required a <c>Running</c>
/// status that nothing could produce, so the endpoint always returned 400.
/// </remarks>
[Collection("Database")]
public sealed class BatchRunTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchRunTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public BatchRunTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Running a batch produces a result per record.
    /// </summary>
    [Fact]
    public async Task Running_A_Batch_Writes_A_Result_Per_Record()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, ["one", "two", "three"]);
        (Guid batchId, DurableJob job) = await SeedBatchAsync(db, datasetId);

        await CreateHandler(db, "the answer").ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        List<BatchResult> results = await verify.Set<BatchResult>()
            .AsNoTracking().Where(r => r.BatchJobId == batchId).ToListAsync();

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(BatchResultStatus.Success, r.Status));
        Assert.All(results, r => Assert.Equal("the answer", r.Output));

        BatchJob batch = await verify.Set<BatchJob>().AsNoTracking().FirstAsync(b => b.Id == batchId);
        Assert.Equal(BatchJobStatus.Completed, batch.Status);
        Assert.Equal(3, batch.CompletedRecords);
        Assert.True(batch.TokensUsed > 0, "Token usage was not accumulated.");
    }

    /// <summary>
    /// Pausing a queued job must be allowed and must actually stop the run.
    /// </summary>
    [Fact]
    public async Task Pause_Is_Permitted_And_Halts_The_Run()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, ["a", "b", "c"]);
        (Guid batchId, DurableJob job) = await SeedBatchAsync(db, datasetId);

        var status = new UpdateBatchJobStatusHandler(db, NullLogger<UpdateBatchJobStatusHandler>.Instance);

        // The pre-fix handler rejected this outright, because it required a status nothing
        // could produce.
        Result<BatchJobDto> paused = await status.HandleAsync(
            new UpdateBatchJobStatusCommand(batchId, "pause"), CancellationToken.None);

        Assert.True(paused.IsSuccess, paused.IsSuccess ? "" : paused.Error.Message);

        await CreateHandler(db, "x").ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        int produced = await verify.Set<BatchResult>().CountAsync(r => r.BatchJobId == batchId);

        Assert.Equal(0, produced);
        Assert.NotEqual(
            BatchJobStatus.Completed,
            (await verify.Set<BatchJob>().AsNoTracking().FirstAsync(b => b.Id == batchId)).Status);
    }

    /// <summary>
    /// Cancelling mid-run must stop the work rather than being noticed only at the end.
    /// </summary>
    [Fact]
    public async Task Cancelling_Stops_The_Run()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, ["a", "b"]);
        (Guid batchId, DurableJob job) = await SeedBatchAsync(db, datasetId);

        var status = new UpdateBatchJobStatusHandler(db, NullLogger<UpdateBatchJobStatusHandler>.Instance);
        await status.HandleAsync(new UpdateBatchJobStatusCommand(batchId, "cancel"), CancellationToken.None);

        await CreateHandler(db, "x").ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        Assert.Equal(0, await verify.Set<BatchResult>().CountAsync(r => r.BatchJobId == batchId));
    }

    /// <summary>
    /// A retried job resumes from where it stopped rather than re-running work already paid for.
    /// </summary>
    /// <remarks>
    /// Exercises the real resume path: a batch left mid-flight with some results already
    /// recorded. An earlier version of this test simply ran the handler twice, which proved
    /// nothing — the second call returned immediately on the Completed guard and never reached
    /// the skip logic at all. Removing the idempotency check did not fail it.
    /// </remarks>
    [Fact]
    public async Task A_Resumed_Batch_Skips_Records_Already_Done()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, ["p", "q", "r"]);
        (Guid batchId, DurableJob job) = await SeedBatchAsync(db, datasetId);

        // Simulate a worker that died after completing the first record.
        DatasetRecord first = await db.Set<DatasetRecord>()
            .Where(r => r.DatasetId == datasetId).OrderBy(r => r.OrderIndex).FirstAsync();

        db.Set<BatchResult>().Add(new BatchResult
        {
            BatchJobId = batchId,
            RecordId = first.Id,
            Input = "p",
            Output = "already done",
            Status = BatchResultStatus.Success,
            TokensUsed = 8,
            Attempt = 1,
        });
        await db.SaveChangesAsync();

        await CreateHandler(db, "fresh").ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        List<BatchResult> results = await verify.Set<BatchResult>()
            .AsNoTracking().Where(r => r.BatchJobId == batchId).ToListAsync();

        Assert.Equal(3, results.Count);
        Assert.Single(results, r => r.RecordId == first.Id);
        Assert.Equal(
            "already done",
            results.Single(r => r.RecordId == first.Id).Output);
    }

    /// <summary>
    /// A provider failure is recorded per record rather than aborting the batch.
    /// </summary>
    [Fact]
    public async Task A_Failing_Record_Is_Recorded_And_Counted()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, ["only"]);
        (Guid batchId, DurableJob job) = await SeedBatchAsync(db, datasetId);

        var handler = new BatchJobHandler(
            db,
            new InferenceProviderFactory(FakeHttpTransport.ServerError(), NullLoggerFactory.Instance),
            NullLogger<BatchJobHandler>.Instance);

        await handler.ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        BatchResult result = await verify.Set<BatchResult>()
            .AsNoTracking().SingleAsync(r => r.BatchJobId == batchId);

        Assert.Equal(BatchResultStatus.Failed, result.Status);
        Assert.NotNull(result.Error);

        BatchJob batch = await verify.Set<BatchJob>().AsNoTracking().FirstAsync(b => b.Id == batchId);
        Assert.Equal(1, batch.FailedRecords);
        Assert.Equal(BatchJobStatus.Completed, batch.Status);
    }

    /// <summary>
    /// Token estimation must scale with the text, not be a constant. The previous estimator
    /// returned the same number for every record regardless of content.
    /// </summary>
    /// <summary>
    /// Retry-failed actually reruns: it enqueues a fresh durable job (the original is
    /// Complete, and nothing watches BatchJob.Status — without this the "retried" job sat
    /// Queued forever), and the rerun reuses the failed rows rather than adding new ones,
    /// so a six-record dataset does not become twelve results with one retry.
    /// </summary>
    [Fact]
    public async Task Retry_Failed_Enqueues_A_Runnable_Job_And_Reuses_Result_Rows()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);
        Guid datasetId = await SeedDatasetAsync(db, ["a", "b"]);
        (Guid batchId, DurableJob job) = await SeedBatchAsync(db, datasetId);

        // First run: everything fails.
        var failing = new BatchJobHandler(
            db,
            new InferenceProviderFactory(FakeHttpTransport.ServerError(), NullLoggerFactory.Instance),
            NullLogger<BatchJobHandler>.Instance);
        await failing.ExecuteAsync(job, CancellationToken.None);

        // Retry: must produce a NEW queued durable job for this batch.
        int durableJobsBefore = await db.Set<DurableJob>().CountAsync(j => j.JobType == BatchJobHandler.Type);
        var retry = new RetryFailedHandler(db, NullLogger<RetryFailedHandler>.Instance);
        Result<BatchJobDto> retried = await retry.HandleAsync(
            new RetryFailedCommand(batchId), CancellationToken.None);
        Assert.True(retried.IsSuccess, retried.IsFailure ? retried.Error.Message : "");

        DurableJob? requeued = await db.Set<DurableJob>()
            .Where(j => j.JobType == BatchJobHandler.Type && j.Status == JobStatus.Queued)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(requeued);
        Assert.True(
            await db.Set<DurableJob>().CountAsync(j => j.JobType == BatchJobHandler.Type) > durableJobsBefore,
            "Retry did not enqueue a new durable job; nothing would ever run the retried batch.");

        // Second run succeeds; rows are reused, not duplicated, and attempts increment.
        await using AppDbContext db2 = _fixture.CreateContext();
        var succeeding = new BatchJobHandler(
            db2,
            new InferenceProviderFactory(
                FakeHttpTransport.ChatCompletion("recovered"), NullLoggerFactory.Instance),
            NullLogger<BatchJobHandler>.Instance);
        await succeeding.ExecuteAsync(requeued, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        List<BatchResult> results = await verify.Set<BatchResult>()
            .AsNoTracking().Where(r => r.BatchJobId == batchId).ToListAsync();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(BatchResultStatus.Success, r.Status));
        Assert.All(results, r => Assert.Equal("recovered", r.Output));
        Assert.All(results, r => Assert.Equal(2, r.Attempt));

        BatchJob batch = await verify.Set<BatchJob>().AsNoTracking().FirstAsync(b => b.Id == batchId);
        Assert.Equal(BatchJobStatus.Completed, batch.Status);
        Assert.Equal(2, batch.CompletedRecords);
        Assert.Equal(0, batch.FailedRecords);
    }

    /// <summary>
    /// The runner uses the default instance, not an arbitrary row. Before the fix it took
    /// the first row EF happened to return — with a dead seeded endpoint inserted first, an
    /// entire batch failed with connection errors while the healthy default sat idle (the
    /// evaluation runner had the identical bug).
    /// </summary>
    [Fact]
    public async Task The_Runner_Uses_The_Default_Instance_Not_An_Arbitrary_Row()
    {
        await using AppDbContext db = _fixture.CreateContext();

        // Serial collection: demote any instances earlier tests left behind, then insert a
        // decoy first so unordered enumeration would find it before the default.
        await db.Set<InferenceInstance>()
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsDefault, false));
        db.Set<InferenceInstance>().Add(new InferenceInstance
        {
            Name = "dead-decoy",
            Endpoint = "http://dead-decoy:1111",
            ProviderType = InferenceProviderType.OpenAiCompatible,
            Status = InstanceStatus.Offline,
        });
        await db.SaveChangesAsync();
        db.Set<InferenceInstance>().Add(new InferenceInstance
        {
            Name = "healthy-default",
            Endpoint = "http://healthy-default:2222",
            ProviderType = InferenceProviderType.OpenAiCompatible,
            Status = InstanceStatus.Online,
            IsDefault = true,
        });
        await db.SaveChangesAsync();

        Guid datasetId = await SeedDatasetAsync(db, ["only record"]);
        (Guid batchId, DurableJob job) = await SeedBatchAsync(db, datasetId);

        var transport = FakeHttpTransport.ChatCompletion("answer");
        var handler = new BatchJobHandler(
            db,
            new InferenceProviderFactory(transport, NullLoggerFactory.Instance),
            NullLogger<BatchJobHandler>.Instance);
        await handler.ExecuteAsync(job, CancellationToken.None);

        Assert.NotEmpty(transport.Requests);
        Assert.All(transport.Requests, r => Assert.Equal(
            "healthy-default", r.RequestUri!.Host));

        await using AppDbContext verify = _fixture.CreateContext();
        BatchJob batch = await verify.Set<BatchJob>().AsNoTracking().FirstAsync(b => b.Id == batchId);
        Assert.Equal(1, batch.CompletedRecords);
    }

    [Fact]
    public void Token_Estimation_Scales_With_Content()
    {
        int shortText = BatchJobHandler.EstimateTokens("hi");
        int longText = BatchJobHandler.EstimateTokens(new string('x', 4000));

        Assert.True(shortText > 0);
        Assert.True(
            longText > shortText * 100,
            $"A 4000-character prompt estimated {longText} tokens against {shortText} for two " +
            "characters; the estimate is not tracking content.");
        Assert.Equal(0, BatchJobHandler.EstimateTokens(""));
    }

    private BatchJobHandler CreateHandler(AppDbContext db, string modelAnswer)
        => new(
            db,
            new InferenceProviderFactory(
                FakeHttpTransport.ChatCompletion(modelAnswer), NullLoggerFactory.Instance),
            NullLogger<BatchJobHandler>.Instance);

    private static async Task SeedInstanceAsync(AppDbContext db)
    {
        if (await db.Set<InferenceInstance>().AnyAsync())
        {
            return;
        }

        db.Set<InferenceInstance>().Add(new InferenceInstance
        {
            Name = "fake",
            Endpoint = "http://localhost:9999",
            ProviderType = InferenceProviderType.OpenAiCompatible,
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedDatasetAsync(AppDbContext db, string[] inputs)
    {
        var dataset = new Dataset { Name = $"batch-{Guid.NewGuid():N}" };
        db.Set<Dataset>().Add(dataset);

        for (int i = 0; i < inputs.Length; i++)
        {
            db.Set<DatasetRecord>().Add(new DatasetRecord
            {
                DatasetId = dataset.Id,
                OrderIndex = i,
                Data = new Dictionary<string, object?> { ["input"] = inputs[i] },
            });
        }

        await db.SaveChangesAsync();
        return dataset.Id;
    }

    private static async Task<(Guid BatchId, DurableJob Job)> SeedBatchAsync(
        AppDbContext db, Guid datasetId)
    {
        int recordCount = await db.Set<DatasetRecord>().CountAsync(r => r.DatasetId == datasetId);

        var batch = new BatchJob
        {
            DatasetId = datasetId,
            Model = "test-model",
            Status = BatchJobStatus.Queued,
            TotalRecords = recordCount,
        };

        db.Set<BatchJob>().Add(batch);
        await db.SaveChangesAsync();

        var job = new DurableJob
        {
            JobType = BatchJobHandler.Type,
            Status = JobStatus.Queued,
            ParametersJson = $$"""{"batchJobId":"{{batch.Id}}"}""",
            TotalItems = recordCount,
        };

        return (batch.Id, job);
    }
}

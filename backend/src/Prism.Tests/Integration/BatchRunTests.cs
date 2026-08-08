using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Jobs;
using Prism.Common.Results;
using Prism.Features.BatchInference.Application.Dtos;
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

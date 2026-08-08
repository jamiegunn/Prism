using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Jobs;
using Prism.Features.Datasets.Domain;
using Prism.Common.Results;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Application.RunEvaluation;
using Prism.Features.Evaluation.Application.StartEvaluation;
using Prism.Features.Evaluation.Domain;
using Prism.Features.Evaluation.Domain.Scorers;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers evaluation actually running: dataset records go to a model, outputs get scored, and
/// a result row exists per record per model.
/// </summary>
/// <remarks>
/// Before this, <c>EvaluationResult</c> was written by zero lines of code. The five scorers
/// were correctly implemented, DI-registered, and called by nothing — so the results,
/// leaderboard and export endpoints returned empty forever regardless of what was run.
/// </remarks>
[Collection("Database")]
public sealed class EvaluationRunTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationRunTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public EvaluationRunTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// The whole point: running an evaluation produces scored results.
    /// </summary>
    [Fact]
    public async Task Running_An_Evaluation_Writes_Scored_Results()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, [("What is 2+2?", "4"), ("Capital of France?", "Paris")]);
        (Guid evaluationId, DurableJob job) = await SeedEvaluationAsync(
            db, datasetId, models: ["test-model"], scorers: ["exact_match", "rouge_l"]);

        // The model answers "4" — right for the first record, wrong for the second.
        EvaluationJobHandler handler = CreateHandler(db, "4");

        await handler.ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        List<EvaluationResult> results = await verify.Set<EvaluationResult>()
            .AsNoTracking()
            .Where(r => r.EvaluationId == evaluationId)
            .ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("test-model", r.Model));

        EvaluationResult correct = results.Single(r => r.Input.StartsWith("What is 2+2", StringComparison.Ordinal));
        EvaluationResult wrong = results.Single(r => r.Input.StartsWith("Capital", StringComparison.Ordinal));

        // The scores must discriminate. A scorer that returns the same number regardless of
        // whether the answer was right is worse than no scorer.
        Assert.Equal(1.0, correct.Scores["exact_match"]);
        Assert.Equal(0.0, wrong.Scores["exact_match"]);
        Assert.True(correct.Scores["rouge_l"] > wrong.Scores["rouge_l"]);

        EvaluationEntity evaluation = await verify.Set<EvaluationEntity>()
            .AsNoTracking().FirstAsync(e => e.Id == evaluationId);
        Assert.Equal(EvaluationStatus.Completed, evaluation.Status);
        Assert.Equal(2, evaluation.CompletedRecords);
    }

    /// <summary>
    /// Every model under test must be evaluated against every record — that product is what
    /// makes a leaderboard possible.
    /// </summary>
    [Fact]
    public async Task Every_Model_Is_Scored_Against_Every_Record()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, [("a", "a"), ("b", "b"), ("c", "c")]);
        (Guid evaluationId, DurableJob job) = await SeedEvaluationAsync(
            db, datasetId, models: ["model-a", "model-b"], scorers: ["exact_match"]);

        await CreateHandler(db, "a").ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        List<EvaluationResult> results = await verify.Set<EvaluationResult>()
            .AsNoTracking().Where(r => r.EvaluationId == evaluationId).ToListAsync();

        Assert.Equal(6, results.Count);
        Assert.Equal(2, results.Select(r => r.Model).Distinct().Count());
        Assert.Equal(3, results.Select(r => r.RecordId).Distinct().Count());
    }

    /// <summary>
    /// A retried job must resume rather than duplicate. Delivery is at-least-once, so a handler
    /// that re-scores everything on retry would double every leaderboard number.
    /// </summary>
    [Fact]
    public async Task Rerunning_A_Job_Does_Not_Duplicate_Results()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, [("x", "x"), ("y", "y")]);
        (Guid evaluationId, DurableJob job) = await SeedEvaluationAsync(
            db, datasetId, models: ["m"], scorers: ["exact_match"]);

        EvaluationJobHandler handler = CreateHandler(db, "x");

        await handler.ExecuteAsync(job, CancellationToken.None);
        await handler.ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        int count = await verify.Set<EvaluationResult>()
            .CountAsync(r => r.EvaluationId == evaluationId);

        Assert.Equal(2, count);
    }

    /// <summary>
    /// A provider failure on one record must be recorded and counted, not allowed to abort the
    /// run or to silently shrink the denominator.
    /// </summary>
    [Fact]
    public async Task A_Failing_Record_Is_Recorded_Rather_Than_Aborting_The_Run()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await SeedInstanceAsync(db);

        Guid datasetId = await SeedDatasetAsync(db, [("q", "a")]);
        (Guid evaluationId, DurableJob job) = await SeedEvaluationAsync(
            db, datasetId, models: ["m"], scorers: ["exact_match"]);

        var handler = new EvaluationJobHandler(
            db,
            new InferenceProviderFactory(
                FakeHttpTransport.ServerError(), NullLoggerFactory.Instance),
            [new ExactMatchScorer()],
            NullLogger<EvaluationJobHandler>.Instance);

        await handler.ExecuteAsync(job, CancellationToken.None);

        await using AppDbContext verify = _fixture.CreateContext();
        EvaluationResult result = await verify.Set<EvaluationResult>()
            .AsNoTracking().SingleAsync(r => r.EvaluationId == evaluationId);

        Assert.NotNull(result.Error);

        EvaluationEntity evaluation = await verify.Set<EvaluationEntity>()
            .AsNoTracking().FirstAsync(e => e.Id == evaluationId);
        Assert.Equal(1, evaluation.FailedRecords);
        Assert.Equal(EvaluationStatus.Completed, evaluation.Status);
    }

    /// <summary>
    /// Dataset records are schemaless, so the handler accepts the field names instruction
    /// datasets actually use rather than mandating one shape.
    /// </summary>
    /// <param name="inputKey">The key holding the prompt.</param>
    /// <param name="expectedKey">The key holding the reference answer.</param>
    [Theory]
    [InlineData("input", "expected")]
    [InlineData("prompt", "output")]
    [InlineData("question", "answer")]
    [InlineData("instruction", "completion")]
    public void Field_Extraction_Accepts_Common_Dataset_Shapes(string inputKey, string expectedKey)
    {
        var record = new DatasetRecord
        {
            Data = new Dictionary<string, object?>
            {
                [inputKey] = "the prompt",
                [expectedKey] = "the answer",
            },
        };

        (string input, string? expected) = EvaluationJobHandler.ExtractFields(record);

        Assert.Equal("the prompt", input);
        Assert.Equal("the answer", expected);
    }

    /// <summary>
    /// A job with no evaluation named in its parameters must fail loudly rather than quietly
    /// completing as if it had done the work.
    /// </summary>
    [Fact]
    public void A_Job_Without_An_Evaluation_Id_Fails_Loudly()
    {
        var job = new DurableJob { JobType = EvaluationJobHandler.Type, ParametersJson = "{}" };

        Assert.Throws<InvalidOperationException>(() => EvaluationJobHandler.ReadEvaluationId(job));
    }

    /// <summary>
    /// Starting an evaluation must enqueue work, not merely record an intention to do it.
    /// </summary>
    /// <remarks>
    /// The original handler's own summary said it "enqueues it for background processing"
    /// while only inserting a Pending row. That gap is the whole reason this module never ran.
    /// </remarks>
    [Fact]
    public async Task Starting_An_Evaluation_Enqueues_A_Job()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid datasetId = await SeedDatasetAsync(db, [("q", "a")]);

        var start = new StartEvaluationHandler(db, NullLogger<StartEvaluationHandler>.Instance);

        Result<EvaluationDto> created = await start.HandleAsync(
            new StartEvaluationCommand(
                Name: $"enqueue-{Guid.NewGuid():N}",
                DatasetId: datasetId,
                SplitLabel: null,
                ProjectId: null,
                Models: ["m"],
                PromptVersionId: null,
                ScoringMethods: ["exact_match"],
                Config: null),
            CancellationToken.None);

        Assert.True(created.IsSuccess);

        await using AppDbContext verify = _fixture.CreateContext();
        List<DurableJob> jobs = await verify.Set<DurableJob>()
            .AsNoTracking()
            .Where(j => j.JobType == EvaluationJobHandler.Type)
            .ToListAsync();

        DurableJob job = Assert.Single(
            jobs,
            j => j.ParametersJson.Contains(created.Value.Id.ToString(), StringComparison.Ordinal));

        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(created.Value.Id, EvaluationJobHandler.ReadEvaluationId(job));
    }

    private EvaluationJobHandler CreateHandler(AppDbContext db, string modelAnswer)
        => new(
            db,
            new InferenceProviderFactory(
                FakeHttpTransport.ChatCompletion(modelAnswer), NullLoggerFactory.Instance),
            [new ExactMatchScorer(), new RougeLScorer()],
            NullLogger<EvaluationJobHandler>.Instance);

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

    private static async Task<Guid> SeedDatasetAsync(
        AppDbContext db, (string Input, string Expected)[] rows)
    {
        var dataset = new Dataset { Name = $"eval-{Guid.NewGuid():N}" };
        db.Set<Dataset>().Add(dataset);

        for (int i = 0; i < rows.Length; i++)
        {
            db.Set<DatasetRecord>().Add(new DatasetRecord
            {
                DatasetId = dataset.Id,
                OrderIndex = i,
                Data = new Dictionary<string, object?>
                {
                    ["input"] = rows[i].Input,
                    ["expected"] = rows[i].Expected,
                },
            });
        }

        await db.SaveChangesAsync();
        return dataset.Id;
    }

    private static async Task<(Guid EvaluationId, DurableJob Job)> SeedEvaluationAsync(
        AppDbContext db, Guid datasetId, string[] models, string[] scorers)
    {
        int recordCount = await db.Set<DatasetRecord>().CountAsync(r => r.DatasetId == datasetId);

        var evaluation = new EvaluationEntity
        {
            Name = $"run-{Guid.NewGuid():N}",
            DatasetId = datasetId,
            Models = [.. models],
            ScoringMethods = [.. scorers],
            Status = EvaluationStatus.Pending,
            TotalRecords = recordCount * models.Length,
        };

        db.Set<EvaluationEntity>().Add(evaluation);
        await db.SaveChangesAsync();

        var job = new DurableJob
        {
            JobType = EvaluationJobHandler.Type,
            Status = JobStatus.Queued,
            ParametersJson = $$"""{"evaluationId":"{{evaluation.Id}}"}""",
            TotalItems = evaluation.TotalRecords,
        };

        return (evaluation.Id, job);
    }
}

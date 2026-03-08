using Microsoft.EntityFrameworkCore;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.BatchInference.Domain;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.BatchInference.Application.CreateBatchJob;

/// <summary>
/// Command to create a new batch inference job.
/// </summary>
public sealed record CreateBatchJobCommand(
    Guid DatasetId,
    string? SplitLabel,
    string Model,
    Guid? PromptVersionId,
    Dictionary<string, object?>? Parameters,
    int Concurrency,
    int MaxRetries,
    bool CaptureLogprobs);

/// <summary>
/// Creates a batch job entity and prepares it for background processing.
/// </summary>
public sealed class CreateBatchJobHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateBatchJobHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateBatchJobHandler"/> class.
    /// </summary>
    public CreateBatchJobHandler(AppDbContext db, ILogger<CreateBatchJobHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Handles the create batch job command.
    /// </summary>
    public async Task<Result<BatchJobDto>> HandleAsync(CreateBatchJobCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Model))
        {
            return Error.Validation("Model is required.");
        }

        bool datasetExists = await _db.Set<Dataset>()
            .AnyAsync(d => d.Id == command.DatasetId, ct);

        if (!datasetExists)
        {
            return Error.NotFound($"Dataset {command.DatasetId} not found.");
        }

        IQueryable<DatasetRecord> recordsQuery = _db.Set<DatasetRecord>()
            .Where(r => r.DatasetId == command.DatasetId);

        if (!string.IsNullOrWhiteSpace(command.SplitLabel))
        {
            recordsQuery = recordsQuery.Where(r => r.SplitLabel == command.SplitLabel);
        }

        int recordCount = await recordsQuery.CountAsync(ct);
        if (recordCount == 0)
        {
            return Error.Validation("No records found in the dataset for the specified split.");
        }

        var job = new BatchJob
        {
            DatasetId = command.DatasetId,
            SplitLabel = command.SplitLabel,
            Model = command.Model,
            PromptVersionId = command.PromptVersionId,
            Parameters = command.Parameters ?? new Dictionary<string, object?>(),
            Concurrency = Math.Max(1, command.Concurrency),
            MaxRetries = Math.Max(0, command.MaxRetries),
            CaptureLogprobs = command.CaptureLogprobs,
            Status = BatchJobStatus.Queued,
            TotalRecords = recordCount
        };

        _db.Set<BatchJob>().Add(job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created batch job {BatchJobId} for model {Model} with {RecordCount} records",
            job.Id, job.Model, recordCount);

        return BatchJobDto.FromEntity(job);
    }
}

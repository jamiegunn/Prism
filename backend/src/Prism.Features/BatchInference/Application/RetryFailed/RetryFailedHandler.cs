using Microsoft.EntityFrameworkCore;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Application.RetryFailed;

/// <summary>
/// Command to retry failed records in a batch job.
/// </summary>
public sealed record RetryFailedCommand(Guid BatchJobId);

/// <summary>
/// Handles retrying failed records in a batch job.
/// </summary>
public sealed class RetryFailedHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<RetryFailedHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryFailedHandler"/> class.
    /// </summary>
    public RetryFailedHandler(AppDbContext db, ILogger<RetryFailedHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Handles the retry failed command.
    /// </summary>
    public async Task<Result<BatchJobDto>> HandleAsync(RetryFailedCommand command, CancellationToken ct)
    {
        BatchJob? job = await _db.Set<BatchJob>()
            .FirstOrDefaultAsync(j => j.Id == command.BatchJobId, ct);

        if (job is null)
        {
            return Error.NotFound($"Batch job {command.BatchJobId} not found.");
        }

        if (job.Status is not (BatchJobStatus.Completed or BatchJobStatus.Failed))
        {
            return Error.Validation("Can only retry failed records on completed or failed jobs.");
        }

        List<BatchResult> failedResults = await _db.Set<BatchResult>()
            .Where(r => r.BatchJobId == command.BatchJobId && r.Status == BatchResultStatus.Failed)
            .ToListAsync(ct);

        if (failedResults.Count == 0)
        {
            return Error.Validation("No failed records to retry.");
        }

        foreach (BatchResult result in failedResults)
        {
            result.Status = BatchResultStatus.Retry;
            result.Error = null;
        }

        job.Status = BatchJobStatus.Queued;
        job.FailedRecords = 0;
        job.FinishedAt = null;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Queued {FailedCount} failed records for retry in batch job {BatchJobId}",
            failedResults.Count, job.Id);

        return BatchJobDto.FromEntity(job);
    }
}

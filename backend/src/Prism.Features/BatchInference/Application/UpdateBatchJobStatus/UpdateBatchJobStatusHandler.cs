using Microsoft.EntityFrameworkCore;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Application.UpdateBatchJobStatus;

/// <summary>
/// Command to update a batch job's status (pause, resume, cancel).
/// </summary>
public sealed record UpdateBatchJobStatusCommand(Guid Id, string Action);

/// <summary>
/// Handles pausing, resuming, or cancelling a batch job.
/// </summary>
public sealed class UpdateBatchJobStatusHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateBatchJobStatusHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateBatchJobStatusHandler"/> class.
    /// </summary>
    public UpdateBatchJobStatusHandler(AppDbContext db, ILogger<UpdateBatchJobStatusHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Handles the update batch job status command.
    /// </summary>
    public async Task<Result<BatchJobDto>> HandleAsync(UpdateBatchJobStatusCommand command, CancellationToken ct)
    {
        BatchJob? job = await _db.Set<BatchJob>()
            .FirstOrDefaultAsync(j => j.Id == command.Id, ct);

        if (job is null)
        {
            return Error.NotFound($"Batch job {command.Id} not found.");
        }

        switch (command.Action.ToLowerInvariant())
        {
            case "pause":
                if (job.Status is not BatchJobStatus.Running)
                {
                    return Error.Validation("Only running jobs can be paused.");
                }
                job.Status = BatchJobStatus.Paused;
                break;

            case "resume":
                if (job.Status is not BatchJobStatus.Paused)
                {
                    return Error.Validation("Only paused jobs can be resumed.");
                }
                job.Status = BatchJobStatus.Running;
                break;

            case "cancel":
                if (job.Status is not (BatchJobStatus.Queued or BatchJobStatus.Running or BatchJobStatus.Paused))
                {
                    return Error.Validation($"Cannot cancel job in {job.Status} status.");
                }
                job.Status = BatchJobStatus.Cancelled;
                job.FinishedAt = DateTime.UtcNow;
                break;

            default:
                return Error.Validation($"Unknown action: {command.Action}. Use 'pause', 'resume', or 'cancel'.");
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated batch job {BatchJobId} to {Status}", job.Id, job.Status);

        return BatchJobDto.FromEntity(job);
    }
}

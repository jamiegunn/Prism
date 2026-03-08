using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.DeleteRun;

/// <summary>
/// Handles deletion of a run from an experiment.
/// </summary>
public sealed class DeleteRunHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteRunHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteRunHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DeleteRunHandler(AppDbContext db, ILogger<DeleteRunHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a run from the database.
    /// </summary>
    /// <param name="command">The delete run command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(DeleteRunCommand command, CancellationToken ct)
    {
        Run? run = await _db.Set<Run>()
            .FirstOrDefaultAsync(r => r.Id == command.RunId && r.ExperimentId == command.ExperimentId, ct);

        if (run is null)
        {
            return Error.NotFound($"Run '{command.RunId}' was not found in experiment '{command.ExperimentId}'.");
        }

        _db.Set<Run>().Remove(run);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted run {RunId} from experiment {ExperimentId}", command.RunId, command.ExperimentId);

        return Result.Success();
    }
}

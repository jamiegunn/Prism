using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ArchiveExperiment;

/// <summary>
/// Handles changing the status of an experiment.
/// </summary>
public sealed class ArchiveExperimentHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<ArchiveExperimentHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchiveExperimentHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ArchiveExperimentHandler(AppDbContext db, ILogger<ArchiveExperimentHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Updates the status of an experiment.
    /// </summary>
    /// <param name="command">The archive experiment command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated experiment DTO on success.</returns>
    public async Task<Result<ExperimentDto>> HandleAsync(ArchiveExperimentCommand command, CancellationToken ct)
    {
        Experiment? experiment = await _db.Set<Experiment>()
            .FirstOrDefaultAsync(e => e.Id == command.ExperimentId, ct);

        if (experiment is null)
        {
            return Error.NotFound($"Experiment '{command.ExperimentId}' was not found.");
        }

        experiment.Status = command.Status;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Changed experiment {ExperimentId} status to {Status}",
            experiment.Id, command.Status);

        int runCount = await _db.Set<Run>()
            .CountAsync(r => r.ExperimentId == experiment.Id, ct);

        return ExperimentDto.FromEntity(experiment, runCount);
    }
}

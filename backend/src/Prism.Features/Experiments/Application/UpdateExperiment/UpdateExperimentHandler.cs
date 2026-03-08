using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.UpdateExperiment;

/// <summary>
/// Handles updating an existing experiment.
/// </summary>
public sealed class UpdateExperimentHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateExperimentHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExperimentHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public UpdateExperimentHandler(AppDbContext db, ILogger<UpdateExperimentHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Updates the name, description, and hypothesis of an existing experiment.
    /// </summary>
    /// <param name="command">The update experiment command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated experiment DTO on success.</returns>
    public async Task<Result<ExperimentDto>> HandleAsync(UpdateExperimentCommand command, CancellationToken ct)
    {
        Experiment? experiment = await _db.Set<Experiment>()
            .FirstOrDefaultAsync(e => e.Id == command.ExperimentId, ct);

        if (experiment is null)
        {
            return Error.NotFound($"Experiment '{command.ExperimentId}' was not found.");
        }

        experiment.Name = command.Name;
        experiment.Description = command.Description;
        experiment.Hypothesis = command.Hypothesis;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated experiment {ExperimentId}", experiment.Id);

        int runCount = await _db.Set<Run>()
            .CountAsync(r => r.ExperimentId == experiment.Id, ct);

        return ExperimentDto.FromEntity(experiment, runCount);
    }
}

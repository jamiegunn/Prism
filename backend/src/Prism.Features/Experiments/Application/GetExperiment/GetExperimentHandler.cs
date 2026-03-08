using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.GetExperiment;

/// <summary>
/// Handles retrieval of a specific experiment by ID.
/// </summary>
public sealed class GetExperimentHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetExperimentHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetExperimentHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves an experiment by its ID, including run count.
    /// </summary>
    /// <param name="query">The query containing the experiment ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the experiment DTO on success.</returns>
    public async Task<Result<ExperimentDto>> HandleAsync(GetExperimentQuery query, CancellationToken ct)
    {
        Experiment? experiment = await _db.Set<Experiment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == query.ExperimentId, ct);

        if (experiment is null)
        {
            return Error.NotFound($"Experiment '{query.ExperimentId}' was not found.");
        }

        int runCount = await _db.Set<Run>()
            .CountAsync(r => r.ExperimentId == experiment.Id, ct);

        return ExperimentDto.FromEntity(experiment, runCount);
    }
}

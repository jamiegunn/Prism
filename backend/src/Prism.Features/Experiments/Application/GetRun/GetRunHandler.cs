using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.GetRun;

/// <summary>
/// Handles retrieval of a specific run by ID.
/// </summary>
public sealed class GetRunHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRunHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetRunHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves a run by its ID within an experiment.
    /// </summary>
    /// <param name="query">The query containing the experiment and run IDs.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the run DTO on success.</returns>
    public async Task<Result<RunDto>> HandleAsync(GetRunQuery query, CancellationToken ct)
    {
        Run? run = await _db.Set<Run>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.RunId && r.ExperimentId == query.ExperimentId, ct);

        if (run is null)
        {
            return Error.NotFound($"Run '{query.RunId}' was not found in experiment '{query.ExperimentId}'.");
        }

        return RunDto.FromEntity(run);
    }
}

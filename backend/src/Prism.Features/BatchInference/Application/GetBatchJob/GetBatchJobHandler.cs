using Microsoft.EntityFrameworkCore;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Application.GetBatchJob;

/// <summary>
/// Query to get a batch job by ID.
/// </summary>
public sealed record GetBatchJobQuery(Guid Id);

/// <summary>
/// Handles getting a single batch job.
/// </summary>
public sealed class GetBatchJobHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetBatchJobHandler"/> class.
    /// </summary>
    public GetBatchJobHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get batch job query.
    /// </summary>
    public async Task<Result<BatchJobDto>> HandleAsync(GetBatchJobQuery query, CancellationToken ct)
    {
        BatchJob? job = await _db.Set<BatchJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == query.Id, ct);

        if (job is null)
        {
            return Error.NotFound($"Batch job {query.Id} not found.");
        }

        return BatchJobDto.FromEntity(job);
    }
}

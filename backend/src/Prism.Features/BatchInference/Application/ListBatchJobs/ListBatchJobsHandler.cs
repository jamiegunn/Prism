using Microsoft.EntityFrameworkCore;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Application.ListBatchJobs;

/// <summary>
/// Query to list batch jobs with optional status filter.
/// </summary>
public sealed record ListBatchJobsQuery(string? Status);

/// <summary>
/// Handles listing batch jobs.
/// </summary>
public sealed class ListBatchJobsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListBatchJobsHandler"/> class.
    /// </summary>
    public ListBatchJobsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the list batch jobs query.
    /// </summary>
    public async Task<Result<List<BatchJobDto>>> HandleAsync(ListBatchJobsQuery query, CancellationToken ct)
    {
        IQueryable<BatchJob> q = _db.Set<BatchJob>()
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt);

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<BatchJobStatus>(query.Status, true, out BatchJobStatus status))
        {
            q = q.Where(j => j.Status == status);
        }

        List<BatchJob> jobs = await q.ToListAsync(ct);
        return jobs.Select(BatchJobDto.FromEntity).ToList();
    }
}

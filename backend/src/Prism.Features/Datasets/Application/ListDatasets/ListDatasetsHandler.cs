using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.ListDatasets;

/// <summary>
/// Query to list datasets with optional filters.
/// </summary>
/// <param name="ProjectId">Optional project filter.</param>
/// <param name="Search">Optional search term for name.</param>
public sealed record ListDatasetsQuery(Guid? ProjectId, string? Search);

/// <summary>
/// Handles listing datasets.
/// </summary>
public sealed class ListDatasetsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListDatasetsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListDatasetsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists datasets matching the provided filters.
    /// </summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of dataset DTOs.</returns>
    public async Task<Result<List<DatasetDto>>> HandleAsync(ListDatasetsQuery query, CancellationToken ct)
    {
        IQueryable<Dataset> q = _db.Set<Dataset>()
            .AsNoTracking()
            .Include(d => d.Splits);

        if (query.ProjectId.HasValue)
        {
            q = q.Where(d => d.ProjectId == query.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.ToLower();
            q = q.Where(d => d.Name.ToLower().Contains(search));
        }

        List<Dataset> datasets = await q.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);

        return datasets.Select(DatasetDto.FromEntity).ToList();
    }
}

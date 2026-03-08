using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.ListInstances;

/// <summary>
/// Handles querying registered inference provider instances with optional filters.
/// </summary>
public sealed class ListInstancesHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListInstancesHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListInstancesHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all registered inference instances, optionally filtered by status and provider type.
    /// </summary>
    /// <param name="query">The query containing optional filters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of matching instance DTOs.</returns>
    public async Task<Result<List<InferenceInstanceDto>>> HandleAsync(ListInstancesQuery query, CancellationToken ct)
    {
        IQueryable<InferenceInstance> queryable = _db.Set<InferenceInstance>().AsNoTracking();

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(i => i.Status == query.Status.Value);
        }

        if (query.ProviderType.HasValue)
        {
            queryable = queryable.Where(i => i.ProviderType == query.ProviderType.Value);
        }

        List<InferenceInstance> instances = await queryable
            .OrderByDescending(i => i.IsDefault)
            .ThenBy(i => i.Name)
            .ToListAsync(ct);

        List<InferenceInstanceDto> dtos = instances
            .Select(InferenceInstanceDto.FromEntity)
            .ToList();

        return dtos;
    }
}

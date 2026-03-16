using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.FineTuning.Application.Dtos;
using Prism.Features.FineTuning.Domain;

namespace Prism.Features.FineTuning.Application.ListAdapters;

/// <summary>
/// Query to list registered LoRA adapters.
/// </summary>
public sealed record ListAdaptersQuery(Guid? InstanceId);

/// <summary>
/// Handles listing LoRA adapters.
/// </summary>
public sealed class ListAdaptersHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListAdaptersHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ListAdaptersHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists LoRA adapters, optionally filtered by instance.
    /// </summary>
    /// <param name="query">The list query parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A list of adapter DTOs.</returns>
    public async Task<Result<List<LoraAdapterDto>>> HandleAsync(ListAdaptersQuery query, CancellationToken ct)
    {
        IQueryable<LoraAdapter> queryable = _db.Set<LoraAdapter>().AsNoTracking();

        if (query.InstanceId.HasValue)
        {
            queryable = queryable.Where(a => a.InstanceId == query.InstanceId.Value);
        }

        List<LoraAdapterDto> adapters = await queryable
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => LoraAdapterDto.FromEntity(a))
            .ToListAsync(ct);

        return adapters;
    }
}

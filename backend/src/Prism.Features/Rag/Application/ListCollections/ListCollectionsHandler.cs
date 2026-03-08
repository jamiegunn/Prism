using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.ListCollections;

/// <summary>
/// Query to list RAG collections with optional filters.
/// </summary>
public sealed record ListCollectionsQuery(Guid? ProjectId, string? Search);

/// <summary>
/// Handles listing RAG collections.
/// </summary>
public sealed class ListCollectionsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListCollectionsHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ListCollectionsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists RAG collections with optional filters.
    /// </summary>
    /// <param name="query">The query parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of collection DTOs.</returns>
    public async Task<Result<List<RagCollectionDto>>> HandleAsync(ListCollectionsQuery query, CancellationToken ct)
    {
        IQueryable<RagCollection> q = _db.Set<RagCollection>().AsNoTracking();

        if (query.ProjectId.HasValue)
            q = q.Where(c => c.ProjectId == query.ProjectId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(c => c.Name.Contains(query.Search) || (c.Description != null && c.Description.Contains(query.Search)));

        List<RagCollection> collections = await q.OrderByDescending(c => c.UpdatedAt).ToListAsync(ct);

        return collections.Select(RagCollectionDto.FromEntity).ToList();
    }
}

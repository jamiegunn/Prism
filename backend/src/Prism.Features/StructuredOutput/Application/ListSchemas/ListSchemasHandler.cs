using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.StructuredOutput.Application.Dtos;
using Prism.Features.StructuredOutput.Domain;

namespace Prism.Features.StructuredOutput.Application.ListSchemas;

/// <summary>
/// Query to list JSON schemas with optional filters.
/// </summary>
public sealed record ListSchemasQuery(Guid? ProjectId, string? Search);

/// <summary>
/// Handles listing JSON schemas.
/// </summary>
public sealed class ListSchemasHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListSchemasHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ListSchemasHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists JSON schemas with optional filters.
    /// </summary>
    /// <param name="query">The query parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of schema DTOs.</returns>
    public async Task<Result<List<JsonSchemaDto>>> HandleAsync(ListSchemasQuery query, CancellationToken ct)
    {
        IQueryable<JsonSchemaEntity> q = _db.Set<JsonSchemaEntity>().AsNoTracking();

        if (query.ProjectId.HasValue)
            q = q.Where(s => s.ProjectId == query.ProjectId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(s => s.Name.Contains(query.Search));

        List<JsonSchemaEntity> schemas = await q.OrderByDescending(s => s.UpdatedAt).ToListAsync(ct);

        return schemas.Select(JsonSchemaDto.FromEntity).ToList();
    }
}

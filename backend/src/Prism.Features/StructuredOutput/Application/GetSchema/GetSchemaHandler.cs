using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.StructuredOutput.Application.Dtos;
using Prism.Features.StructuredOutput.Domain;

namespace Prism.Features.StructuredOutput.Application.GetSchema;

/// <summary>
/// Query to get a specific JSON schema by ID.
/// </summary>
public sealed record GetSchemaQuery(Guid Id);

/// <summary>
/// Handles retrieval of a single JSON schema.
/// </summary>
public sealed class GetSchemaHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetSchemaHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public GetSchemaHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Gets a JSON schema by ID.
    /// </summary>
    /// <param name="query">The query containing the schema ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the schema DTO or a not-found error.</returns>
    public async Task<Result<JsonSchemaDto>> HandleAsync(GetSchemaQuery query, CancellationToken ct)
    {
        JsonSchemaEntity? schema = await _db.Set<JsonSchemaEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.Id, ct);

        if (schema is null)
            return Error.NotFound($"JSON schema {query.Id} not found.");

        return JsonSchemaDto.FromEntity(schema);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.StructuredOutput.Domain;

namespace Prism.Features.StructuredOutput.Application.DeleteSchema;

/// <summary>
/// Command to delete a JSON schema.
/// </summary>
public sealed record DeleteSchemaCommand(Guid Id);

/// <summary>
/// Handles deletion of a JSON schema.
/// </summary>
public sealed class DeleteSchemaHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteSchemaHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteSchemaHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DeleteSchemaHandler(AppDbContext db, ILogger<DeleteSchemaHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a JSON schema.
    /// </summary>
    /// <param name="command">The deletion command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(DeleteSchemaCommand command, CancellationToken ct)
    {
        JsonSchemaEntity? schema = await _db.Set<JsonSchemaEntity>()
            .FirstOrDefaultAsync(s => s.Id == command.Id, ct);

        if (schema is null)
            return Error.NotFound($"JSON schema {command.Id} not found.");

        _db.Set<JsonSchemaEntity>().Remove(schema);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted JSON schema {SchemaName} ({SchemaId})", schema.Name, schema.Id);

        return Result.Success();
    }
}

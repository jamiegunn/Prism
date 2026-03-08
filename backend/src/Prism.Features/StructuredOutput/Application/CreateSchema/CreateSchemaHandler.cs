using System.Text.Json;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.StructuredOutput.Application.Dtos;
using Prism.Features.StructuredOutput.Domain;

namespace Prism.Features.StructuredOutput.Application.CreateSchema;

/// <summary>
/// Command to create a new JSON schema.
/// </summary>
public sealed record CreateSchemaCommand(
    string Name,
    string? Description,
    string SchemaJson,
    Guid? ProjectId);

/// <summary>
/// Handles creation of a new JSON schema.
/// </summary>
public sealed class CreateSchemaHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateSchemaHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSchemaHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateSchemaHandler(AppDbContext db, ILogger<CreateSchemaHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new JSON schema.
    /// </summary>
    /// <param name="command">The creation command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created schema DTO.</returns>
    public async Task<Result<JsonSchemaDto>> HandleAsync(CreateSchemaCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Error.Validation("Schema name is required.");

        // Validate the schema is valid JSON
        try
        {
            JsonDocument.Parse(command.SchemaJson);
        }
        catch (JsonException ex)
        {
            return Error.Validation($"Invalid JSON schema: {ex.Message}");
        }

        var schema = new JsonSchemaEntity
        {
            Name = command.Name,
            Description = command.Description,
            SchemaJson = command.SchemaJson,
            ProjectId = command.ProjectId
        };

        _db.Set<JsonSchemaEntity>().Add(schema);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created JSON schema {SchemaName}", schema.Name);

        return JsonSchemaDto.FromEntity(schema);
    }
}

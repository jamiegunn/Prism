namespace Prism.Features.StructuredOutput.Api.Requests;

/// <summary>
/// Request body for creating a JSON schema.
/// </summary>
public sealed record CreateSchemaRequest(
    string Name,
    string? Description,
    string SchemaJson,
    Guid? ProjectId);

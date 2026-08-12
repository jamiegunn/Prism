using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.StructuredOutput.Application.CreateSchema;
using Prism.Features.StructuredOutput.Application.Dtos;

namespace Prism.Tests.Integration;

/// <summary>
/// Proofs for JSON-schema creation validation. <c>JsonDocument.Parse</c> throws
/// <see cref="ArgumentNullException"/> — not <see cref="System.Text.Json.JsonException"/> — on
/// null, so a missing <c>schemaJson</c> slipped past the handler's catch and became a 500. A
/// non-object schema (a bare string or number) parsed fine but is unusable for guided decoding.
/// </summary>
[Collection("Database")]
public sealed class CreateSchemaTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSchemaTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public CreateSchemaTests(DatabaseFixture fixture) => _fixture = fixture;

    private CreateSchemaHandler Handler() =>
        new(_fixture.CreateContext(), NullLogger<CreateSchemaHandler>.Instance);

    private async Task<Result<JsonSchemaDto>> CreateAsync(string name, string? schemaJson) =>
        await Handler().HandleAsync(
            new CreateSchemaCommand(name, null, schemaJson!, null), CancellationToken.None);

    /// <summary>
    /// A null or blank schema is a validation error, not the ArgumentNullException-shaped 500
    /// it used to be.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_Schema_Is_A_Validation_Error(string? schemaJson)
    {
        Result<JsonSchemaDto> result = await CreateAsync("s", schemaJson);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    /// <summary>
    /// Malformed JSON is rejected with a validation error carrying the parser's reason.
    /// </summary>
    [Fact]
    public async Task Malformed_Json_Is_A_Validation_Error()
    {
        Result<JsonSchemaDto> result = await CreateAsync("s", "not json {");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("Invalid JSON schema", result.Error.Message);
    }

    /// <summary>
    /// Valid JSON that is not an object — a bare string or number — is rejected, because it
    /// cannot drive guided decoding.
    /// </summary>
    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("42")]
    [InlineData("[1,2,3]")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task Non_Object_Schema_Is_A_Validation_Error(string schemaJson)
    {
        Result<JsonSchemaDto> result = await CreateAsync("s", schemaJson);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("object", result.Error.Message);
    }

    /// <summary>
    /// A well-formed object schema is accepted and persisted.
    /// </summary>
    [Fact]
    public async Task Valid_Object_Schema_Is_Created()
    {
        Result<JsonSchemaDto> result = await CreateAsync(
            $"schema-{Guid.NewGuid():N}",
            """{"type":"object","properties":{"x":{"type":"string"}}}""");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : "");
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    /// <summary>
    /// A blank name is still rejected — the pre-existing guard is not regressed by the new ones.
    /// </summary>
    [Fact]
    public async Task Blank_Name_Is_A_Validation_Error()
    {
        Result<JsonSchemaDto> result = await CreateAsync("  ", """{"type":"object"}""");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }
}

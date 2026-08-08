using Prism.Features.StructuredOutput.Domain;

namespace Prism.Tests.Unit.StructuredOutput;

/// <summary>
/// Covers JSON Schema validation.
/// </summary>
/// <remarks>
/// The previous check inspected only top-level <c>required</c> and <c>type</c>, never
/// recursed, and swallowed schema parse failures in a bare <c>catch</c> — so a nested
/// violation, or an unparseable schema, reported the output as valid. For a tool whose job is
/// to tell a researcher whether the model obeyed a contract, a false "valid" is the worst
/// possible answer.
/// </remarks>
public sealed class JsonSchemaValidatorTests
{
    private const string PersonSchema = """
        {
          "type": "object",
          "required": ["name", "age"],
          "properties": {
            "name": { "type": "string", "minLength": 1 },
            "age": { "type": "integer", "minimum": 0, "maximum": 150 },
            "email": { "type": "string" },
            "address": {
              "type": "object",
              "required": ["city"],
              "properties": {
                "city": { "type": "string" },
                "postcode": { "type": "string", "maxLength": 8 }
              }
            },
            "tags": {
              "type": "array",
              "minItems": 1,
              "items": { "type": "string" }
            },
            "status": { "enum": ["active", "inactive"] }
          }
        }
        """;

    [Fact]
    public void A_Conforming_Document_Is_Valid()
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate(
            """
            {"name":"Ada","age":36,"address":{"city":"London","postcode":"NW1"},
             "tags":["mathematician"],"status":"active"}
            """,
            PersonSchema);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void A_Missing_Required_Property_Is_Reported()
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate("""{"name":"Ada"}""", PersonSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("age", StringComparison.Ordinal));
    }

    /// <summary>
    /// The failure the old validator could not see: the document is fine at the top level and
    /// wrong inside a nested object.
    /// </summary>
    [Fact]
    public void A_Nested_Violation_Is_Reported()
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate(
            """{"name":"Ada","age":36,"address":{"postcode":"NW1"}}""",
            PersonSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("address", StringComparison.Ordinal)
                                            && e.Contains("city", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Nested_Type_Error_Is_Reported()
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate(
            """{"name":"Ada","age":36,"address":{"city":123}}""",
            PersonSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("address.city", StringComparison.Ordinal));
    }

    [Fact]
    public void An_Array_Item_Violation_Is_Reported()
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate(
            """{"name":"Ada","age":36,"tags":["ok",42]}""",
            PersonSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("tags[1]", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"name":"Ada","age":36,"tags":[]}""", "minItems")]
    [InlineData("""{"name":"","age":36}""", "minLength")]
    [InlineData("""{"name":"Ada","age":-1}""", "minimum")]
    [InlineData("""{"name":"Ada","age":200}""", "maximum")]
    [InlineData("""{"name":"Ada","age":36,"status":"lapsed"}""", "enum")]
    public void Constraint_Violations_Are_Reported(string document, string expectedMention)
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate(document, PersonSchema);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.Contains(expectedMention, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// JSON Schema's integer type is mathematical: 36.0 is an integer, 36.5 is not.
    /// </summary>
    [Theory]
    [InlineData("36", true)]
    [InlineData("36.0", true)]
    [InlineData("36.5", false)]
    public void Integer_Means_Whole_Number_Not_Literal_Form(string age, bool expectedValid)
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate(
            $$"""{"name":"Ada","age":{{age}}}""", PersonSchema);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Additional_Properties_Are_Rejected_When_Forbidden()
    {
        const string strict = """
            {"type":"object","additionalProperties":false,
             "properties":{"a":{"type":"string"}}}
            """;

        SchemaValidationResult result = JsonSchemaValidator.Validate("""{"a":"x","b":"y"}""", strict);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("'b'", StringComparison.Ordinal));
    }

    [Fact]
    public void Output_That_Is_Not_Json_Is_Invalid()
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate(
            "Sure! Here is the JSON you asked for:", PersonSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not valid JSON", StringComparison.Ordinal));
    }

    /// <summary>
    /// An unusable schema must fail loudly. The old implementation caught the parse error and
    /// returned no errors, which the caller then read as "the output conformed".
    /// </summary>
    [Fact]
    public void An_Unparseable_Schema_Fails_Rather_Than_Passing_Silently()
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate("""{"a":1}""", "{ not json");

        Assert.False(result.IsValid);
        Assert.NotNull(result.SchemaError);
    }

    /// <summary>
    /// A keyword this validator cannot evaluate must be surfaced rather than ignored, so a
    /// "valid" verdict never rests on a check that silently did not happen.
    /// </summary>
    [Fact]
    public void An_Unsupported_Keyword_Is_Surfaced_Not_Ignored()
    {
        const string schema = """
            {"type":"object","properties":{"a":{"anyOf":[{"type":"string"},{"type":"number"}]}}}
            """;

        SchemaValidationResult result = JsonSchemaValidator.Validate("""{"a":"x"}""", schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("anyOf", StringComparison.Ordinal));
    }

    /// <summary>
    /// An unknown type name is a schema defect. Treating it as "matches anything" is how the
    /// old validator turned typos into passes.
    /// </summary>
    [Fact]
    public void An_Unknown_Type_Name_Does_Not_Match_Everything()
    {
        SchemaValidationResult result = JsonSchemaValidator.Validate(
            """{"a":"x"}""",
            """{"type":"object","properties":{"a":{"type":"strng"}}}""");

        Assert.False(result.IsValid);
    }
}

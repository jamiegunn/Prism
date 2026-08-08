using System.Globalization;
using System.Text.Json;

namespace Prism.Features.StructuredOutput.Domain;

/// <summary>
/// Validates a JSON document against a JSON Schema subset.
/// </summary>
/// <remarks>
/// <para>
/// Replaces a check that looked only at top-level <c>required</c> and <c>type</c>, ignored
/// nesting entirely, and wrapped itself in <c>catch { }</c> so an unparseable schema silently
/// reported the output as valid. Silently passing is the worst possible answer here: the point
/// of the feature is to tell a researcher whether the model obeyed the contract.
/// </para>
/// <para>
/// Deliberately a subset, not a conformant implementation — no <c>$ref</c>, no
/// <c>allOf</c>/<c>anyOf</c>/<c>oneOf</c>, no format assertions. It covers what guided decoding
/// schemas actually use, and reports an unsupported keyword rather than pretending to have
/// checked it. A full implementation belongs in a dedicated library once one can be restored
/// from the package feed.
/// </para>
/// </remarks>
public static class JsonSchemaValidator
{
    private static readonly string[] UnsupportedKeywords =
        ["$ref", "allOf", "anyOf", "oneOf", "not", "if", "patternProperties", "dependentSchemas"];

    /// <summary>
    /// Validates an instance document against a schema.
    /// </summary>
    /// <param name="instanceJson">The document to validate.</param>
    /// <param name="schemaJson">The JSON Schema.</param>
    /// <returns>
    /// The validation outcome. An unparseable schema is an error, not an excuse to pass.
    /// </returns>
    public static SchemaValidationResult Validate(string instanceJson, string schemaJson)
    {
        JsonDocument schema;

        try
        {
            schema = JsonDocument.Parse(schemaJson);
        }
        catch (JsonException ex)
        {
            return SchemaValidationResult.SchemaInvalid($"The schema itself is not valid JSON: {ex.Message}");
        }

        using (schema)
        {
            JsonDocument instance;

            try
            {
                instance = JsonDocument.Parse(instanceJson);
            }
            catch (JsonException ex)
            {
                return SchemaValidationResult.Invalid([$"Output is not valid JSON: {ex.Message}"]);
            }

            using (instance)
            {
                var errors = new List<string>();
                ValidateValue(instance.RootElement, schema.RootElement, "$", errors);

                return errors.Count == 0
                    ? SchemaValidationResult.Valid()
                    : SchemaValidationResult.Invalid(errors);
            }
        }
    }

    private static void ValidateValue(
        JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (string keyword in UnsupportedKeywords)
        {
            if (schema.TryGetProperty(keyword, out _))
            {
                errors.Add(
                    $"{path}: schema uses '{keyword}', which this validator does not support. " +
                    "The result cannot be trusted either way.");
            }
        }

        if (schema.TryGetProperty("type", out JsonElement typeElement))
        {
            if (!MatchesType(value, typeElement, out string expected))
            {
                errors.Add($"{path}: expected type '{expected}' but found '{Describe(value)}'.");

                // A wrong type makes every nested check meaningless, so stop here rather than
                // emitting a cascade of confusing follow-on errors.
                return;
            }
        }

        if (schema.TryGetProperty("enum", out JsonElement enumElement)
            && enumElement.ValueKind == JsonValueKind.Array)
        {
            bool matched = enumElement.EnumerateArray()
                .Any(allowed => JsonElement.DeepEquals(allowed, value));

            if (!matched)
            {
                errors.Add($"{path}: value is not one of the values listed in enum.");
            }
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(value, schema, path, errors);
                break;
            case JsonValueKind.Array:
                ValidateArray(value, schema, path, errors);
                break;
            case JsonValueKind.String:
                ValidateString(value, schema, path, errors);
                break;
            case JsonValueKind.Number:
                ValidateNumber(value, schema, path, errors);
                break;
            default:
                break;
        }
    }

    private static void ValidateObject(
        JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (schema.TryGetProperty("required", out JsonElement required)
            && required.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement name in required.EnumerateArray())
            {
                string field = name.GetString() ?? string.Empty;

                if (!value.TryGetProperty(field, out _))
                {
                    errors.Add($"{path}: missing required property '{field}'.");
                }
            }
        }

        if (!schema.TryGetProperty("properties", out JsonElement properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty property in properties.EnumerateObject())
        {
            if (value.TryGetProperty(property.Name, out JsonElement child))
            {
                // Recursion is the whole point: the previous check stopped at the top level, so
                // a nested object could violate its schema entirely and still report valid.
                ValidateValue(child, property.Value, $"{path}.{property.Name}", errors);
            }
        }

        if (schema.TryGetProperty("additionalProperties", out JsonElement additional)
            && additional.ValueKind == JsonValueKind.False)
        {
            foreach (JsonProperty actual in value.EnumerateObject())
            {
                if (!properties.TryGetProperty(actual.Name, out _))
                {
                    errors.Add($"{path}: unexpected property '{actual.Name}' (additionalProperties is false).");
                }
            }
        }
    }

    private static void ValidateArray(
        JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        int length = value.GetArrayLength();

        if (schema.TryGetProperty("minItems", out JsonElement minItems)
            && minItems.TryGetInt32(out int min) && length < min)
        {
            errors.Add($"{path}: minItems is {min} but found {length} items.");
        }

        if (schema.TryGetProperty("maxItems", out JsonElement maxItems)
            && maxItems.TryGetInt32(out int max) && length > max)
        {
            errors.Add($"{path}: maxItems is {max} but found {length} items.");
        }

        if (!schema.TryGetProperty("items", out JsonElement items))
        {
            return;
        }

        int index = 0;

        foreach (JsonElement element in value.EnumerateArray())
        {
            ValidateValue(element, items, $"{path}[{index}]", errors);
            index++;
        }
    }

    private static void ValidateString(
        JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        string text = value.GetString() ?? string.Empty;

        if (schema.TryGetProperty("minLength", out JsonElement minLength)
            && minLength.TryGetInt32(out int min) && text.Length < min)
        {
            errors.Add($"{path}: minLength is {min} but the string has {text.Length} characters.");
        }

        if (schema.TryGetProperty("maxLength", out JsonElement maxLength)
            && maxLength.TryGetInt32(out int max) && text.Length > max)
        {
            errors.Add($"{path}: maxLength is {max} but the string has {text.Length} characters.");
        }
    }

    private static void ValidateNumber(
        JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        double number = value.GetDouble();

        if (schema.TryGetProperty("minimum", out JsonElement minimum)
            && minimum.TryGetDouble(out double min) && number < min)
        {
            errors.Add($"{path}: minimum is {min} but the value is {number.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (schema.TryGetProperty("maximum", out JsonElement maximum)
            && maximum.TryGetDouble(out double max) && number > max)
        {
            errors.Add($"{path}: maximum is {max} but the value is {number.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static bool MatchesType(JsonElement value, JsonElement typeElement, out string expected)
    {
        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            List<string> names = [.. typeElement.EnumerateArray().Select(t => t.GetString() ?? "")];
            expected = string.Join(" or ", names);
            return names.Any(name => MatchesTypeName(value, name));
        }

        expected = typeElement.GetString() ?? string.Empty;
        return MatchesTypeName(value, expected);
    }

    private static bool MatchesTypeName(JsonElement value, string typeName) => typeName switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number,

        // JSON Schema treats 1.0 as an integer; the distinction is mathematical, not lexical.
        "integer" => value.ValueKind == JsonValueKind.Number
                     && value.TryGetDouble(out double d)
                     && Math.Abs(d % 1) < double.Epsilon,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "array" => value.ValueKind == JsonValueKind.Array,
        "object" => value.ValueKind == JsonValueKind.Object,
        "null" => value.ValueKind == JsonValueKind.Null,

        // An unrecognised type name is a schema defect. Returning true would quietly pass
        // everything, which is how the previous implementation hid problems.
        _ => false,
    };

    private static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Number => "number",
        JsonValueKind.String => "string",
        JsonValueKind.Array => "array",
        JsonValueKind.Object => "object",
        JsonValueKind.Null => "null",
        _ => "undefined",
    };
}

/// <summary>
/// The outcome of validating a document against a schema.
/// </summary>
/// <param name="IsValid">Whether the document satisfied the schema.</param>
/// <param name="Errors">Human-readable failures, each prefixed with a JSON path.</param>
/// <param name="SchemaError">
/// Set when the schema itself could not be used. Distinct from <paramref name="Errors"/>:
/// the output was never actually checked, which the caller must not confuse with it passing.
/// </param>
public sealed record SchemaValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    string? SchemaError)
{
    /// <summary>Creates a passing result.</summary>
    /// <returns>A valid result.</returns>
    public static SchemaValidationResult Valid() => new(true, [], null);

    /// <summary>Creates a failing result.</summary>
    /// <param name="errors">The failures.</param>
    /// <returns>An invalid result.</returns>
    public static SchemaValidationResult Invalid(IReadOnlyList<string> errors) => new(false, errors, null);

    /// <summary>Creates a result for a schema that could not be used at all.</summary>
    /// <param name="message">What was wrong with the schema.</param>
    /// <returns>An invalid result carrying the schema error.</returns>
    public static SchemaValidationResult SchemaInvalid(string message) => new(false, [message], message);
}

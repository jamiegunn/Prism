using System.Text.Json.Serialization;

namespace Prism.Common.Extensions;

/// <summary>
/// Provides helper methods for JSON serialization and deserialization using System.Text.Json.
/// </summary>
public static class JsonExtensions
{
    /// <summary>
    /// Default JSON serializer options used throughout the application.
    /// Configured with camelCase naming, enum string conversion, and lenient reading.
    /// </summary>
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Default JSON serializer options with indented output for human-readable formatting.
    /// </summary>
    public static readonly JsonSerializerOptions IndentedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serializes an object to a JSON string using the default options.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A JSON string representation of the object.</returns>
    public static string ToJson<T>(this T value) =>
        JsonSerializer.Serialize(value, DefaultOptions);

    /// <summary>
    /// Serializes an object to a JSON string with indented formatting.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>An indented JSON string representation of the object.</returns>
    public static string ToJsonIndented<T>(this T value) =>
        JsonSerializer.Serialize(value, IndentedOptions);

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type using default options.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object, or default if deserialization fails.</returns>
    public static T? FromJson<T>(this string json) =>
        JsonSerializer.Deserialize<T>(json, DefaultOptions);

    /// <summary>
    /// Attempts to deserialize a JSON string, returning a boolean indicating success.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="value">When successful, the deserialized value; otherwise, default.</param>
    /// <returns>True if deserialization succeeded; otherwise, false.</returns>
    public static bool TryFromJson<T>(this string json, out T? value)
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(json, DefaultOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Serializes an object to a UTF-8 byte array using default options.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A UTF-8 encoded byte array of the JSON representation.</returns>
    public static byte[] ToJsonBytes<T>(this T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, DefaultOptions);

    /// <summary>
    /// Deserializes a UTF-8 byte array to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="utf8Json">The UTF-8 encoded JSON byte array.</param>
    /// <returns>The deserialized object, or default if deserialization fails.</returns>
    public static T? FromJsonBytes<T>(this ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize<T>(utf8Json, DefaultOptions);
}

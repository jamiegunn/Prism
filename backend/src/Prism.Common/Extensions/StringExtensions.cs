using System.Text;
using System.Text.RegularExpressions;

namespace Prism.Common.Extensions;

/// <summary>
/// Provides common string utility extension methods.
/// </summary>
public static partial class StringExtensions
{
    /// <summary>
    /// Converts a PascalCase or camelCase string to snake_case.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The snake_case representation of the string.</returns>
    /// <example>
    /// "MyPropertyName".ToSnakeCase() returns "my_property_name"
    /// </example>
    public static string ToSnakeCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        StringBuilder builder = new();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && !char.IsUpper(value[i - 1]))
                {
                    builder.Append('_');
                }
                else if (i > 0 && i < value.Length - 1 && char.IsUpper(value[i - 1]) && char.IsLower(value[i + 1]))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Truncates a string to the specified maximum length, appending an ellipsis suffix if truncated.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="maxLength">The maximum length of the returned string, including the suffix.</param>
    /// <param name="suffix">The suffix to append when truncation occurs. Defaults to "...".</param>
    /// <returns>The original string if within the limit, or a truncated string with the suffix appended.</returns>
    public static string Truncate(this string value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        int truncatedLength = maxLength - suffix.Length;
        if (truncatedLength <= 0)
        {
            return suffix[..maxLength];
        }

        return string.Concat(value.AsSpan(0, truncatedLength), suffix);
    }

    /// <summary>
    /// Returns null if the string is null, empty, or consists only of whitespace characters.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns>The original string if it has content; otherwise, null.</returns>
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// Removes all non-alphanumeric characters from a string, keeping only letters, digits, and the specified allowed characters.
    /// </summary>
    /// <param name="value">The string to sanitize.</param>
    /// <param name="allowedChars">Additional characters to allow beyond alphanumeric.</param>
    /// <returns>A sanitized string containing only allowed characters.</returns>
    public static string Sanitize(this string value, params char[] allowedChars)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        HashSet<char> allowed = new(allowedChars);
        StringBuilder builder = new(value.Length);
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c) || allowed.Contains(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Converts a string to a URL-friendly slug using lowercase letters, digits, and hyphens.
    /// </summary>
    /// <param name="value">The string to slugify.</param>
    /// <returns>A URL-friendly slug.</returns>
    /// <example>
    /// "Hello World! Test 123".ToSlug() returns "hello-world-test-123"
    /// </example>
    public static string ToSlug(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string slug = value.ToLowerInvariant();
        slug = SlugInvalidCharsRegex().Replace(slug, "");
        slug = SlugWhitespaceRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        return slug;
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex SlugInvalidCharsRegex();

    [GeneratedRegex(@"[\s-]+")]
    private static partial Regex SlugWhitespaceRegex();
}

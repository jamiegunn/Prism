using System.Text.RegularExpressions;

namespace Prism.Features.Rag.Domain.Parsing;

/// <summary>
/// Parses HTML files by stripping tags and extracting text content.
/// Uses regex-based tag stripping to avoid heavy NuGet dependencies.
/// </summary>
public sealed partial class HtmlParser : IDocumentParser
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedContentTypes { get; } =
        ["text/html", "application/xhtml+xml"];

    [GeneratedRegex(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ScriptTags();

    [GeneratedRegex(@"<style[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex StyleTags();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTags();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpaces();

    /// <inheritdoc />
    public async Task<string> ParseAsync(Stream stream, string filename, CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        string html = await reader.ReadToEndAsync(ct);

        // Remove script and style blocks
        string cleaned = ScriptTags().Replace(html, " ");
        cleaned = StyleTags().Replace(cleaned, " ");

        // Strip remaining HTML tags
        cleaned = HtmlTags().Replace(cleaned, " ");

        // Decode common HTML entities
        cleaned = cleaned
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'");

        // Normalize whitespace
        cleaned = MultipleSpaces().Replace(cleaned, " ");

        return cleaned.Trim();
    }
}

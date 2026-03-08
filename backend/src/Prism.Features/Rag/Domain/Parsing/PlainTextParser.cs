namespace Prism.Features.Rag.Domain.Parsing;

/// <summary>
/// Parses plain text and Markdown files by reading their content directly.
/// </summary>
public sealed class PlainTextParser : IDocumentParser
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedContentTypes { get; } =
        ["text/plain", "text/markdown", "application/octet-stream"];

    /// <inheritdoc />
    public async Task<string> ParseAsync(Stream stream, string filename, CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
}

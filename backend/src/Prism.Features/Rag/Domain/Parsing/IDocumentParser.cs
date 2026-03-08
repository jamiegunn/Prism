namespace Prism.Features.Rag.Domain.Parsing;

/// <summary>
/// Extracts plain text from a document of a specific format.
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// Gets the content types this parser handles (e.g., "text/plain", "text/markdown").
    /// </summary>
    IReadOnlyList<string> SupportedContentTypes { get; }

    /// <summary>
    /// Extracts plain text content from the provided stream.
    /// </summary>
    /// <param name="stream">The document content stream.</param>
    /// <param name="filename">The original filename for format heuristics.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The extracted plain text.</returns>
    Task<string> ParseAsync(Stream stream, string filename, CancellationToken ct);
}

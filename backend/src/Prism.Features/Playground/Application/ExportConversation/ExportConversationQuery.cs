namespace Prism.Features.Playground.Application.ExportConversation;

/// <summary>
/// Query to export a playground conversation in a specified format.
/// </summary>
/// <param name="Id">The conversation identifier to export.</param>
/// <param name="Format">The desired export format.</param>
public sealed record ExportConversationQuery(Guid Id, ExportFormat Format);

/// <summary>
/// Supported export formats for playground conversations.
/// </summary>
public enum ExportFormat
{
    /// <summary>Full JSON export with structured logprobs data.</summary>
    Json,

    /// <summary>Formatted Markdown chat transcript.</summary>
    Markdown,

    /// <summary>JSON Lines format with one message per line.</summary>
    Jsonl
}

/// <summary>
/// Contains the exported conversation content, MIME type, and suggested filename.
/// </summary>
/// <param name="Content">The exported content as a string.</param>
/// <param name="ContentType">The MIME content type of the export.</param>
/// <param name="FileName">The suggested filename for download.</param>
public sealed record ExportResult(string Content, string ContentType, string FileName);

namespace Prism.Common.Export;

/// <summary>
/// Specifies the output format for data exports.
/// </summary>
public enum ExportFormat
{
    /// <summary>Comma-separated values format.</summary>
    Csv,

    /// <summary>JSON format (pretty-printed).</summary>
    Json,

    /// <summary>JSON Lines format (one JSON object per line).</summary>
    Jsonl,

    /// <summary>Markdown format.</summary>
    Markdown,

    /// <summary>Single-file HTML report.</summary>
    Html
}

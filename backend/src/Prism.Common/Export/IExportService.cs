using Prism.Common.Results;

namespace Prism.Common.Export;

/// <summary>
/// Defines the export service contract for converting data to various output formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports a collection of data items to the specified format.
    /// </summary>
    /// <typeparam name="T">The type of data items to export.</typeparam>
    /// <param name="data">The collection of items to export.</param>
    /// <param name="format">The desired output format.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing a stream of the exported data.</returns>
    Task<Result<Stream>> ExportAsync<T>(IReadOnlyList<T> data, ExportFormat format, CancellationToken ct);
}

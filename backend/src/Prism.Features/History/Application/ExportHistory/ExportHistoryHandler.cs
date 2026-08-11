using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Prism.Common.Abstractions;
using Prism.Features.History.Application.SearchHistory;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Application.ExportHistory;

/// <summary>
/// Exports filtered history records as JSONL, CSV or Parquet, streaming rows from the database
/// to the output as they arrive rather than assembling the file in memory — a 100k-row export
/// must not allocate 100k rows.
/// </summary>
/// <remarks>
/// <para>
/// Null handling is the load-bearing decision in this file. A metric that was not measured is
/// exported as null (JSONL <c>null</c>, Parquet null, CSV empty field), never as <c>0</c> or
/// <c>""</c>. In CSV, where the format itself has no null literal, the convention is: a
/// completely empty field is null; an empty <em>quoted</em> field (<c>""</c>) is an empty
/// string. Every non-null string field is quoted, so the two cases cannot collide.
/// </para>
/// <para>
/// Timestamps are exported in UTC. Parquet stores them with millisecond precision
/// (<see cref="DateTimeFormat.DateAndTime"/>); JSONL and CSV carry the full ISO-8601
/// round-trip form.
/// </para>
/// </remarks>
public sealed class ExportHistoryHandler
{
    /// <summary>
    /// Rows buffered per Parquet row group. Bounds export memory: only this many rows are ever
    /// held at once, regardless of how many the filters select.
    /// </summary>
    internal const int ParquetRowGroupSize = 2000;

    private readonly AppDbContext _db;
    private readonly ILogger<ExportHistoryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportHistoryHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ExportHistoryHandler(AppDbContext db, ILogger<ExportHistoryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Validates the requested format, counts the rows the filters select, and returns a
    /// deferred writer that streams those rows to a stream in the requested format.
    /// </summary>
    /// <param name="query">The export query: filters plus format.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The export descriptor, or a validation error for an unknown format.</returns>
    public async Task<Result<HistoryExport>> HandleAsync(ExportHistoryQuery query, CancellationToken ct)
    {
        string format = query.Format.Trim().ToLowerInvariant();

        if (format is not ("jsonl" or "csv" or "parquet"))
        {
            return Error.Validation(
                $"Invalid format '{query.Format}'. Supported formats: jsonl, csv, parquet.");
        }

        IQueryable<InferenceRecord> filtered = HistoryFilters.Apply(
            _db.Set<InferenceRecord>().AsNoTracking(), query.Filters);

        long rowCount = await filtered.LongCountAsync(ct);

        // Deterministic order so an export is reproducible; Id breaks StartedAt ties.
        IQueryable<InferenceRecord> ordered = filtered
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.Id);

        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        _logger.LogInformation(
            "Exporting {RowCount} history records as {Format}", rowCount, format);

        HistoryExport export = format switch
        {
            "jsonl" => new HistoryExport(
                "application/jsonl",
                $"history-export-{stamp}.jsonl",
                rowCount,
                (stream, token) => WriteJsonlAsync(RowsAsync(ordered, token), stream, token)),
            "csv" => new HistoryExport(
                "text/csv",
                $"history-export-{stamp}.csv",
                rowCount,
                (stream, token) => WriteCsvAsync(RowsAsync(ordered, token), stream, token)),
            _ => new HistoryExport(
                "application/vnd.apache.parquet",
                $"history-export-{stamp}.parquet",
                rowCount,
                (stream, token) => WriteParquetAsync(RowsAsync(ordered, token), stream, token)),
        };

        return export;
    }

    /// <summary>
    /// Streams matching records from the database one at a time.
    /// </summary>
    /// <param name="ordered">The filtered, ordered queryable.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The export rows, in export order.</returns>
    private static async IAsyncEnumerable<HistoryExportRow> RowsAsync(
        IQueryable<InferenceRecord> ordered,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (InferenceRecord record in ordered.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return HistoryExportRow.FromEntity(record);
        }
    }

    private static readonly JsonSerializerOptions JsonlOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Writes one JSON object per line. Nulls are written explicitly so a consumer can tell
    /// "not measured" from "absent field".
    /// </summary>
    /// <param name="rows">The rows to write.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    private static async Task WriteJsonlAsync(
        IAsyncEnumerable<HistoryExportRow> rows, Stream output, CancellationToken ct)
    {
        await foreach (HistoryExportRow row in rows.WithCancellation(ct))
        {
            byte[] line = JsonSerializer.SerializeToUtf8Bytes(row, JsonlOptions);
            await output.WriteAsync(line, ct);
            await output.WriteAsync("\n"u8.ToArray(), ct);
        }

        await output.FlushAsync(ct);
    }

    /// <summary>
    /// The CSV header, in the same order the row fields are written.
    /// </summary>
    internal static readonly string[] CsvColumns =
    [
        "id", "sourceModule", "providerName", "providerType", "providerEndpoint", "model",
        "requestJson", "responseJson", "promptTokens", "completionTokens", "totalTokens",
        "latencyMs", "ttftMs", "perplexity", "meanEntropy", "surpriseTokenCount",
        "tokensPerSecond", "estimatedCost", "isSuccess", "errorMessage", "tags",
        "startedAt", "completedAt",
    ];

    /// <summary>
    /// Writes RFC-4180 CSV. Non-null strings are always quoted; null renders as an empty
    /// field, which is what distinguishes a null <c>responseJson</c> from an empty one.
    /// </summary>
    /// <param name="rows">The rows to write.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    private static async Task WriteCsvAsync(
        IAsyncEnumerable<HistoryExportRow> rows, Stream output, CancellationToken ct)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);

        await writer.WriteLineAsync(string.Join(",", CsvColumns));

        await foreach (HistoryExportRow row in rows.WithCancellation(ct))
        {
            string[] fields =
            [
                Quote(row.Id.ToString()),
                Quote(row.SourceModule),
                Quote(row.ProviderName),
                Quote(row.ProviderType),
                Quote(row.ProviderEndpoint),
                Quote(row.Model),
                Quote(row.RequestJson),
                QuoteOrNull(row.ResponseJson),
                Invariant(row.PromptTokens),
                Invariant(row.CompletionTokens),
                Invariant(row.TotalTokens),
                Invariant(row.LatencyMs),
                row.TtftMs is null ? "" : Invariant(row.TtftMs.Value),
                row.Perplexity is null ? "" : Roundtrip(row.Perplexity.Value),
                row.MeanEntropy is null ? "" : Roundtrip(row.MeanEntropy.Value),
                row.SurpriseTokenCount is null ? "" : Invariant(row.SurpriseTokenCount.Value),
                row.TokensPerSecond is null ? "" : Roundtrip(row.TokensPerSecond.Value),
                row.EstimatedCost is null
                    ? ""
                    : row.EstimatedCost.Value.ToString(CultureInfo.InvariantCulture),
                row.IsSuccess ? "true" : "false",
                QuoteOrNull(row.ErrorMessage),
                Quote(JsonSerializer.Serialize(row.Tags)),
                Iso(row.StartedAt),
                Iso(row.CompletedAt),
            ];

            await writer.WriteLineAsync(string.Join(",", fields));
        }

        await writer.FlushAsync(ct);
    }

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a double with the round-trip format so re-parsing yields the identical bits.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted value.</returns>
    private static string Roundtrip(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// Quotes a non-null string, doubling embedded quotes per RFC 4180.
    /// </summary>
    /// <param name="value">The string to quote.</param>
    /// <returns>The quoted field.</returns>
    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static string QuoteOrNull(string? value) => value is null ? "" : Quote(value);

    /// <summary>
    /// Builds the Parquet schema. Field names, types and nullability mirror
    /// <see cref="HistoryExportRow"/> one to one; the round-trip test asserts that
    /// correspondence field by field.
    /// </summary>
    /// <returns>The schema.</returns>
    internal static ParquetSchema BuildParquetSchema() => new(
        // String fields state nullability explicitly: Parquet.Net defaults every
        // reference-type field to nullable, and a schema that says "id may be null" when it
        // never can is a wrong schema.
        new DataField("id", typeof(string), isNullable: false),
        new DataField("sourceModule", typeof(string), isNullable: false),
        new DataField("providerName", typeof(string), isNullable: false),
        new DataField("providerType", typeof(string), isNullable: false),
        new DataField("providerEndpoint", typeof(string), isNullable: false),
        new DataField("model", typeof(string), isNullable: false),
        new DataField("requestJson", typeof(string), isNullable: false),
        new DataField("responseJson", typeof(string), isNullable: true),
        new DataField<int>("promptTokens"),
        new DataField<int>("completionTokens"),
        new DataField<int>("totalTokens"),
        new DataField<long>("latencyMs"),
        new DataField<int?>("ttftMs"),
        new DataField<double?>("perplexity"),
        new DataField<double?>("meanEntropy"),
        new DataField<int?>("surpriseTokenCount"),
        new DataField<double?>("tokensPerSecond"),
        new DecimalDataField("estimatedCost", precision: 18, scale: 8, isNullable: true),
        new DataField<bool>("isSuccess"),
        new DataField("errorMessage", typeof(string), isNullable: true),
        new DataField("tags", typeof(string), isNullable: false),
        new DateTimeDataField("startedAt", DateTimeFormat.DateAndTime),
        new DateTimeDataField("completedAt", DateTimeFormat.DateAndTime));

    /// <summary>
    /// Writes Parquet in row groups of <see cref="ParquetRowGroupSize"/> rows, so memory use is
    /// bounded by the group size, not the export size. Tags are a JSON-encoded string column —
    /// a flat schema reads everywhere, where Parquet list columns still do not.
    /// </summary>
    /// <param name="rows">The rows to write.</param>
    /// <param name="output">The destination stream.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    private static async Task WriteParquetAsync(
        IAsyncEnumerable<HistoryExportRow> rows, Stream output, CancellationToken ct)
    {
        ParquetSchema schema = BuildParquetSchema();

        using ParquetWriter writer = await ParquetWriter.CreateAsync(schema, output, cancellationToken: ct);
        writer.CompressionMethod = CompressionMethod.Snappy;

        var batch = new List<HistoryExportRow>(ParquetRowGroupSize);

        await foreach (HistoryExportRow row in rows.WithCancellation(ct))
        {
            batch.Add(row);

            if (batch.Count == ParquetRowGroupSize)
            {
                await WriteRowGroupAsync(writer, schema, batch, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await WriteRowGroupAsync(writer, schema, batch, ct);
        }

        await output.FlushAsync(ct);
    }

    /// <summary>
    /// Writes one buffered batch as a Parquet row group.
    /// </summary>
    /// <param name="writer">The open Parquet writer.</param>
    /// <param name="schema">The schema the writer was created with.</param>
    /// <param name="batch">The rows in this group.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    private static async Task WriteRowGroupAsync(
        ParquetWriter writer,
        ParquetSchema schema,
        List<HistoryExportRow> batch,
        CancellationToken ct)
    {
        DataField[] fields = schema.DataFields;

        using ParquetRowGroupWriter group = writer.CreateRowGroup();

        Array[] columns =
        [
            batch.Select(r => r.Id.ToString()).ToArray(),
            batch.Select(r => r.SourceModule).ToArray(),
            batch.Select(r => r.ProviderName).ToArray(),
            batch.Select(r => r.ProviderType).ToArray(),
            batch.Select(r => r.ProviderEndpoint).ToArray(),
            batch.Select(r => r.Model).ToArray(),
            batch.Select(r => r.RequestJson).ToArray(),
            batch.Select(r => r.ResponseJson).ToArray(),
            batch.Select(r => r.PromptTokens).ToArray(),
            batch.Select(r => r.CompletionTokens).ToArray(),
            batch.Select(r => r.TotalTokens).ToArray(),
            batch.Select(r => r.LatencyMs).ToArray(),
            batch.Select(r => r.TtftMs).ToArray(),
            batch.Select(r => r.Perplexity).ToArray(),
            batch.Select(r => r.MeanEntropy).ToArray(),
            batch.Select(r => r.SurpriseTokenCount).ToArray(),
            batch.Select(r => r.TokensPerSecond).ToArray(),
            batch.Select(r => r.EstimatedCost).ToArray(),
            batch.Select(r => r.IsSuccess).ToArray(),
            batch.Select(r => r.ErrorMessage).ToArray(),
            batch.Select(r => JsonSerializer.Serialize(r.Tags)).ToArray(),
            batch.Select(r => DateTime.SpecifyKind(r.StartedAt, DateTimeKind.Utc)).ToArray(),
            batch.Select(r => DateTime.SpecifyKind(r.CompletedAt, DateTimeKind.Utc)).ToArray(),
        ];

        for (int i = 0; i < fields.Length; i++)
        {
            await group.WriteColumnAsync(new DataColumn(fields[i], columns[i]), ct);
        }
    }
}

/// <summary>
/// A prepared export: content metadata, the row count the filters selected, and a deferred
/// writer that streams the file body.
/// </summary>
/// <param name="ContentType">The MIME type of the export.</param>
/// <param name="FileName">The suggested download file name.</param>
/// <param name="RowCount">How many rows the filters selected, counted before writing.</param>
/// <param name="WriteAsync">Writes the file body to a stream.</param>
public sealed record HistoryExport(
    string ContentType,
    string FileName,
    long RowCount,
    Func<Stream, CancellationToken, Task> WriteAsync);

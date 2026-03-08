using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.ExportDataset;

/// <summary>
/// Query to export dataset records in a specified format.
/// </summary>
/// <param name="DatasetId">The dataset identifier.</param>
/// <param name="Format">The export format (csv, json, jsonl).</param>
/// <param name="SplitLabel">Optional split to export.</param>
public sealed record ExportDatasetQuery(Guid DatasetId, string Format, string? SplitLabel);

/// <summary>
/// The result of a dataset export operation.
/// </summary>
/// <param name="Data">The exported data as bytes.</param>
/// <param name="ContentType">The MIME content type.</param>
/// <param name="FileName">The suggested file name.</param>
public sealed record ExportResult(byte[] Data, string ContentType, string FileName);

/// <summary>
/// Handles exporting dataset records.
/// </summary>
public sealed class ExportDatasetHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportDatasetHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ExportDatasetHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Exports dataset records in the requested format.
    /// </summary>
    /// <param name="query">The export query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the export data.</returns>
    public async Task<Result<ExportResult>> HandleAsync(ExportDatasetQuery query, CancellationToken ct)
    {
        Dataset? dataset = await _db.Set<Dataset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == query.DatasetId, ct);

        if (dataset is null)
        {
            return Error.NotFound($"Dataset {query.DatasetId} not found.");
        }

        IQueryable<DatasetRecord> recordsQuery = _db.Set<DatasetRecord>()
            .AsNoTracking()
            .Where(r => r.DatasetId == query.DatasetId)
            .OrderBy(r => r.OrderIndex);

        if (!string.IsNullOrWhiteSpace(query.SplitLabel))
        {
            recordsQuery = recordsQuery.Where(r => r.SplitLabel == query.SplitLabel);
        }

        List<DatasetRecord> records = await recordsQuery.ToListAsync(ct);
        string splitSuffix = query.SplitLabel is not null ? $"_{query.SplitLabel}" : "";

        return query.Format.ToLowerInvariant() switch
        {
            "csv" => ExportCsv(dataset, records, splitSuffix),
            "jsonl" => ExportJsonl(dataset, records, splitSuffix),
            _ => ExportJson(dataset, records, splitSuffix),
        };
    }

    private static ExportResult ExportCsv(Dataset dataset, List<DatasetRecord> records, string splitSuffix)
    {
        var sb = new StringBuilder();
        List<string> columns = dataset.Schema.Select(c => c.Name).ToList();

        sb.AppendLine(string.Join(",", columns.Select(EscapeCsv)));

        foreach (DatasetRecord record in records)
        {
            var values = columns.Select(col =>
            {
                if (record.Data.TryGetValue(col, out object? val) && val is not null)
                {
                    string s = val is JsonElement je ? je.ToString() : val.ToString() ?? "";
                    return EscapeCsv(s);
                }
                return "";
            });
            sb.AppendLine(string.Join(",", values));
        }

        return new ExportResult(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            $"{dataset.Name}{splitSuffix}.csv");
    }

    private static ExportResult ExportJson(Dataset dataset, List<DatasetRecord> records, string splitSuffix)
    {
        var data = records.Select(r => r.Data).ToList();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true });

        return new ExportResult(bytes, "application/json", $"{dataset.Name}{splitSuffix}.json");
    }

    private static ExportResult ExportJsonl(Dataset dataset, List<DatasetRecord> records, string splitSuffix)
    {
        var sb = new StringBuilder();
        foreach (DatasetRecord record in records)
        {
            sb.AppendLine(JsonSerializer.Serialize(record.Data));
        }

        return new ExportResult(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "application/jsonl",
            $"{dataset.Name}{splitSuffix}.jsonl");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

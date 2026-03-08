using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.UploadDataset;

/// <summary>
/// Handles dataset file upload, parsing, and persistence.
/// </summary>
public sealed class UploadDatasetHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UploadDatasetHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadDatasetHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public UploadDatasetHandler(AppDbContext db, ILogger<UploadDatasetHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Uploads, parses, and stores a dataset from the provided file stream.
    /// </summary>
    /// <param name="command">The upload command containing file data.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created dataset DTO.</returns>
    public async Task<Result<DatasetDto>> HandleAsync(UploadDatasetCommand command, CancellationToken ct)
    {
        DatasetFormat format = DetectFormat(command.FileName);

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = await ParseFileAsync(command.ContentStream, format, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse dataset file {FileName}", command.FileName);
            return Error.Validation($"Failed to parse file: {ex.Message}");
        }

        if (rows.Count == 0)
        {
            return Error.Validation("File contains no records.");
        }

        // Auto-detect schema from first record
        List<ColumnSchema> schema = DetectSchema(rows[0]);

        var dataset = new Dataset
        {
            Name = command.Name,
            Description = command.Description,
            ProjectId = command.ProjectId,
            Format = format,
            Schema = schema,
            RecordCount = rows.Count,
            SizeBytes = command.ContentLength,
        };

        _db.Set<Dataset>().Add(dataset);

        // Insert records
        for (int i = 0; i < rows.Count; i++)
        {
            var record = new DatasetRecord
            {
                DatasetId = dataset.Id,
                Data = rows[i],
                OrderIndex = i,
            };
            _db.Set<DatasetRecord>().Add(record);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Uploaded dataset {DatasetId} with {RecordCount} records from {FileName}",
            dataset.Id, rows.Count, command.FileName);

        return DatasetDto.FromEntity(dataset);
    }

    private static DatasetFormat DetectFormat(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".csv" => DatasetFormat.Csv,
            ".json" => DatasetFormat.Json,
            ".jsonl" => DatasetFormat.Jsonl,
            ".parquet" => DatasetFormat.Parquet,
            _ => DatasetFormat.Csv,
        };
    }

    private static async Task<List<Dictionary<string, object?>>> ParseFileAsync(
        Stream stream, DatasetFormat format, CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        string content = await reader.ReadToEndAsync(ct);

        return format switch
        {
            DatasetFormat.Csv => ParseCsv(content),
            DatasetFormat.Json => ParseJson(content),
            DatasetFormat.Jsonl => ParseJsonl(content),
            _ => throw new NotSupportedException($"Format {format} is not yet supported for upload."),
        };
    }

    private static List<Dictionary<string, object?>> ParseCsv(string content)
    {
        var rows = new List<Dictionary<string, object?>>();
        string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2) return rows;

        string[] headers = ParseCsvLine(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, object?>();
            for (int j = 0; j < headers.Length && j < values.Length; j++)
            {
                row[headers[j]] = string.IsNullOrEmpty(values[j]) ? null : (object)values[j];
            }
            rows.Add(row);
        }

        return rows;
    }

    private static string[] ParseCsvLine(string line)
    {
        // Simple CSV parser — handles basic quoted fields
        var fields = new List<string>();
        bool inQuotes = false;
        int start = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                fields.Add(line[start..i].Trim().Trim('"'));
                start = i + 1;
            }
        }
        fields.Add(line[start..].Trim().Trim('"').TrimEnd('\r'));

        return fields.ToArray();
    }

    private static List<Dictionary<string, object?>> ParseJson(string content)
    {
        var items = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return items ?? [];
    }

    private static List<Dictionary<string, object?>> ParseJsonl(string content)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (string line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var row = JsonSerializer.Deserialize<Dictionary<string, object?>>(trimmed,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (row is not null)
            {
                rows.Add(row);
            }
        }
        return rows;
    }

    private static List<ColumnSchema> DetectSchema(Dictionary<string, object?> sample)
    {
        return sample.Select(kvp => new ColumnSchema
        {
            Name = kvp.Key,
            Type = InferType(kvp.Value),
        }).ToList();
    }

    private static string InferType(object? value)
    {
        if (value is null) return "string";
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Array => "array",
                JsonValueKind.Object => "object",
                _ => "string",
            };
        }
        return value switch
        {
            int or long or float or double or decimal => "number",
            bool => "boolean",
            _ => "string",
        };
    }
}

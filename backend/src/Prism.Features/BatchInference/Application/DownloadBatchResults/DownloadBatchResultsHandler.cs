using System.Text;
using Microsoft.EntityFrameworkCore;
using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Application.DownloadBatchResults;

/// <summary>
/// Query to download batch results as a file.
/// </summary>
public sealed record DownloadBatchResultsQuery(Guid BatchJobId, string Format);

/// <summary>
/// Result of downloading batch results.
/// </summary>
public sealed record DownloadResultData(byte[] Data, string ContentType, string FileName);

/// <summary>
/// Handles downloading batch results as CSV or JSON.
/// </summary>
public sealed class DownloadBatchResultsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadBatchResultsHandler"/> class.
    /// </summary>
    public DownloadBatchResultsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the download batch results query.
    /// </summary>
    public async Task<Result<DownloadResultData>> HandleAsync(DownloadBatchResultsQuery query, CancellationToken ct)
    {
        BatchJob? job = await _db.Set<BatchJob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == query.BatchJobId, ct);

        if (job is null)
        {
            return Error.NotFound($"Batch job {query.BatchJobId} not found.");
        }

        List<BatchResult> results = await _db.Set<BatchResult>()
            .AsNoTracking()
            .Where(r => r.BatchJobId == query.BatchJobId && r.Status == BatchResultStatus.Success)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return query.Format.ToLowerInvariant() switch
        {
            "csv" => ExportCsv(job, results),
            "jsonl" => ExportJsonl(job, results),
            "json" => ExportJson(job, results),
            _ => Error.Validation($"Unsupported format: {query.Format}. Use 'csv', 'json', or 'jsonl'.")
        };
    }

    private static DownloadResultData ExportCsv(BatchJob job, List<BatchResult> results)
    {
        StringBuilder sb = new();
        sb.AppendLine("RecordId,Input,Output,TokensUsed,LatencyMs,Perplexity");

        foreach (BatchResult r in results)
        {
            sb.Append(r.RecordId).Append(',');
            sb.Append(CsvEscape(r.Input)).Append(',');
            sb.Append(CsvEscape(r.Output ?? "")).Append(',');
            sb.Append(r.TokensUsed).Append(',');
            sb.Append(r.LatencyMs).Append(',');
            sb.Append(r.Perplexity?.ToString() ?? "");
            sb.AppendLine();
        }

        string fileName = $"batch_{job.Model.Replace('/', '_')}_{DateTime.UtcNow:yyyyMMdd}.csv";
        return new DownloadResultData(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    private static DownloadResultData ExportJsonl(BatchJob job, List<BatchResult> results)
    {
        StringBuilder sb = new();
        foreach (BatchResult r in results)
        {
            string line = JsonSerializer.Serialize(new
            {
                r.RecordId,
                r.Input,
                r.Output,
                r.TokensUsed,
                r.LatencyMs,
                r.Perplexity
            });
            sb.AppendLine(line);
        }

        string fileName = $"batch_{job.Model.Replace('/', '_')}_{DateTime.UtcNow:yyyyMMdd}.jsonl";
        return new DownloadResultData(Encoding.UTF8.GetBytes(sb.ToString()), "application/jsonl", fileName);
    }

    private static DownloadResultData ExportJson(BatchJob job, List<BatchResult> results)
    {
        var data = results.Select(r => new
        {
            r.RecordId,
            r.Input,
            r.Output,
            r.TokensUsed,
            r.LatencyMs,
            r.Perplexity
        });

        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        string fileName = $"batch_{job.Model.Replace('/', '_')}_{DateTime.UtcNow:yyyyMMdd}.json";
        return new DownloadResultData(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

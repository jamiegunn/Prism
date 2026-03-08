using System.Text;
using Microsoft.EntityFrameworkCore;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.ExportResults;

/// <summary>
/// Query to export evaluation results in a specified format.
/// </summary>
public sealed record ExportResultsQuery(Guid EvaluationId, string Format, string? Model);

/// <summary>
/// Result of exporting evaluation results.
/// </summary>
public sealed record ExportResultsData(byte[] Data, string ContentType, string FileName);

/// <summary>
/// Handles exporting evaluation results to CSV or JSON.
/// </summary>
public sealed class ExportResultsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportResultsHandler"/> class.
    /// </summary>
    public ExportResultsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the export results query.
    /// </summary>
    public async Task<Result<ExportResultsData>> HandleAsync(ExportResultsQuery query, CancellationToken ct)
    {
        EvaluationEntity? evaluation = await _db.Set<EvaluationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == query.EvaluationId, ct);

        if (evaluation is null)
        {
            return Error.NotFound($"Evaluation {query.EvaluationId} not found.");
        }

        IQueryable<EvaluationResult> q = _db.Set<EvaluationResult>()
            .AsNoTracking()
            .Where(r => r.EvaluationId == query.EvaluationId);

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            q = q.Where(r => r.Model == query.Model);
        }

        List<EvaluationResult> results = await q.OrderBy(r => r.CreatedAt).ToListAsync(ct);

        return query.Format.ToLowerInvariant() switch
        {
            "csv" => ExportCsv(evaluation, results),
            "json" => ExportJson(results),
            _ => Error.Validation($"Unsupported export format: {query.Format}. Use 'csv' or 'json'.")
        };
    }

    private static ExportResultsData ExportCsv(EvaluationEntity evaluation, List<EvaluationResult> results)
    {
        // Collect all unique score keys
        HashSet<string> scoreKeys = new();
        foreach (EvaluationResult r in results)
        {
            foreach (string key in r.Scores.Keys)
            {
                scoreKeys.Add(key);
            }
        }

        List<string> sortedKeys = scoreKeys.OrderBy(k => k).ToList();

        StringBuilder sb = new();
        sb.Append("Model,RecordId,Input,Expected,Actual,LatencyMs,PromptTokens,CompletionTokens,Error");
        foreach (string key in sortedKeys)
        {
            sb.Append(',').Append(key);
        }
        sb.AppendLine();

        foreach (EvaluationResult r in results)
        {
            sb.Append(CsvEscape(r.Model)).Append(',');
            sb.Append(r.RecordId).Append(',');
            sb.Append(CsvEscape(r.Input)).Append(',');
            sb.Append(CsvEscape(r.ExpectedOutput ?? "")).Append(',');
            sb.Append(CsvEscape(r.ActualOutput ?? "")).Append(',');
            sb.Append(r.LatencyMs).Append(',');
            sb.Append(r.PromptTokens).Append(',');
            sb.Append(r.CompletionTokens).Append(',');
            sb.Append(CsvEscape(r.Error ?? ""));
            foreach (string key in sortedKeys)
            {
                sb.Append(',').Append(r.Scores.GetValueOrDefault(key));
            }
            sb.AppendLine();
        }

        string fileName = $"evaluation_{evaluation.Name.Replace(' ', '_')}_{DateTime.UtcNow:yyyyMMdd}.csv";
        return new ExportResultsData(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    private static ExportResultsData ExportJson(List<EvaluationResult> results)
    {
        var exportData = results.Select(r => new
        {
            r.Model,
            r.RecordId,
            r.Input,
            r.ExpectedOutput,
            r.ActualOutput,
            r.Scores,
            r.LatencyMs,
            r.PromptTokens,
            r.CompletionTokens,
            r.Perplexity,
            r.Error
        });

        string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        return new ExportResultsData(Encoding.UTF8.GetBytes(json), "application/json", "evaluation_results.json");
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

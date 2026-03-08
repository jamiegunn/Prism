using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ExportRuns;

/// <summary>
/// Handles exporting experiment runs in CSV or JSON format.
/// </summary>
public sealed class ExportRunsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportRunsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ExportRunsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Exports all runs in an experiment as CSV or JSON.
    /// </summary>
    /// <param name="query">The query containing experiment ID and format.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the export data with content type and filename.</returns>
    public async Task<Result<ExportResult>> HandleAsync(ExportRunsQuery query, CancellationToken ct)
    {
        bool experimentExists = await _db.Set<Experiment>()
            .AnyAsync(e => e.Id == query.ExperimentId, ct);

        if (!experimentExists)
        {
            return Error.NotFound($"Experiment '{query.ExperimentId}' was not found.");
        }

        List<Run> runs = await _db.Set<Run>()
            .AsNoTracking()
            .Where(r => r.ExperimentId == query.ExperimentId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return query.Format.ToLowerInvariant() switch
        {
            "csv" => ExportCsv(runs, query.ExperimentId),
            "json" => ExportJson(runs, query.ExperimentId),
            _ => Error.Validation($"Invalid format '{query.Format}'. Supported formats: csv, json.")
        };
    }

    private static ExportResult ExportCsv(List<Run> runs, Guid experimentId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,Model,Status,PromptTokens,CompletionTokens,TotalTokens,LatencyMs,TtftMs,TokensPerSecond,Perplexity,Cost,FinishReason,CreatedAt");

        foreach (Run run in runs)
        {
            sb.AppendLine(string.Join(",",
                run.Id,
                EscapeCsv(run.Name ?? ""),
                EscapeCsv(run.Model),
                run.Status,
                run.PromptTokens,
                run.CompletionTokens,
                run.TotalTokens,
                run.LatencyMs,
                run.TtftMs?.ToString() ?? "",
                run.TokensPerSecond?.ToString("F2") ?? "",
                run.Perplexity?.ToString("F4") ?? "",
                run.Cost?.ToString("F8") ?? "",
                EscapeCsv(run.FinishReason ?? ""),
                run.CreatedAt.ToString("o")));
        }

        return new ExportResult(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            $"experiment-{experimentId}-runs.csv");
    }

    private static ExportResult ExportJson(List<Run> runs, Guid experimentId)
    {
        List<RunDto> dtos = runs.Select(RunDto.FromEntity).ToList();
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(dtos, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new ExportResult(data, "application/json", $"experiment-{experimentId}-runs.json");
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

/// <summary>
/// The result of a run export operation.
/// </summary>
/// <param name="Data">The exported file content.</param>
/// <param name="ContentType">The MIME content type.</param>
/// <param name="FileName">The suggested filename.</param>
public sealed record ExportResult(byte[] Data, string ContentType, string FileName);

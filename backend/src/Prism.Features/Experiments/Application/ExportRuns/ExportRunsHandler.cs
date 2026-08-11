using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.ExportRuns;

/// <summary>
/// Handles exporting experiment runs in CSV, JSON or MLflow-compatible format.
/// </summary>
/// <remarks>
/// <para>
/// The CSV used to omit input, output, parameters, tags and custom metrics — a temperature
/// sweep's CSV did not contain its temperatures (P6 in <c>docs/features/experiments.md</c>).
/// It now carries every parameter as a <c>param.*</c> column, every custom metric as a
/// <c>metric.*</c> column (the union across runs; a run missing a metric leaves the cell
/// empty, never 0), plus input, output, system prompt, tags and error.
/// </para>
/// <para>
/// The <c>mlflow</c> format emits one document per run in the exact shape of
/// <c>mlflow.entities.Run.to_dictionary()</c> — <c>{"info": {...}, "data": {"params":…,
/// "metrics":…, "tags":…}}</c> — so a notebook can replay them into a real tracking store
/// via <c>MlflowClient</c> and read them back. Params are strings and metrics doubles, as
/// MLflow requires; a null Prism value is omitted rather than zeroed.
/// </para>
/// </remarks>
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
    /// Exports all runs in an experiment as CSV, JSON or MLflow-compatible JSON.
    /// </summary>
    /// <param name="query">The query containing experiment ID and format.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the export data with content type and filename.</returns>
    public async Task<Result<ExportResult>> HandleAsync(ExportRunsQuery query, CancellationToken ct)
    {
        Experiment? experiment = await _db.Set<Experiment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == query.ExperimentId, ct);

        if (experiment is null)
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
            "mlflow" => ExportMlflow(runs, experiment),
            _ => Error.Validation(
                $"Invalid format '{query.Format}'. Supported formats: csv, json, mlflow."),
        };
    }

    private static ExportResult ExportCsv(List<Run> runs, Guid experimentId)
    {
        // The union of custom metric keys across all runs, sorted for a stable header.
        List<string> metricKeys = runs
            .SelectMany(r => r.Metrics.Keys)
            .Distinct()
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        string[] fixedColumns =
        [
            "Id", "Name", "Model", "Status",
            "param.temperature", "param.topP", "param.topK", "param.maxTokens",
            "param.frequencyPenalty", "param.presencePenalty", "param.stopSequences",
            "PromptTokens", "CompletionTokens", "TotalTokens", "LatencyMs", "TtftMs",
            "TokensPerSecond", "Perplexity", "Cost", "FinishReason", "Tags",
            "Input", "SystemPrompt", "Output", "Error", "CreatedAt",
        ];

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",",
            fixedColumns.Concat(metricKeys.Select(k => $"metric.{k}"))));

        foreach (Run run in runs)
        {
            RunParameters p = run.Parameters;

            IEnumerable<string> fields = new[]
            {
                run.Id.ToString(),
                EscapeCsv(run.Name ?? ""),
                EscapeCsv(run.Model),
                run.Status.ToString(),
                p.Temperature?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                p.TopP?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                p.TopK?.ToString(CultureInfo.InvariantCulture) ?? "",
                p.MaxTokens?.ToString(CultureInfo.InvariantCulture) ?? "",
                p.FrequencyPenalty?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                p.PresencePenalty?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                p.StopSequences is { Count: > 0 } ? EscapeCsv(JsonSerializer.Serialize(p.StopSequences)) : "",
                run.PromptTokens.ToString(CultureInfo.InvariantCulture),
                run.CompletionTokens.ToString(CultureInfo.InvariantCulture),
                run.TotalTokens.ToString(CultureInfo.InvariantCulture),
                run.LatencyMs.ToString(CultureInfo.InvariantCulture),
                run.TtftMs?.ToString(CultureInfo.InvariantCulture) ?? "",
                run.TokensPerSecond?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                run.Perplexity?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                run.Cost?.ToString(CultureInfo.InvariantCulture) ?? "",
                EscapeCsv(run.FinishReason ?? ""),
                run.Tags.Count > 0 ? EscapeCsv(JsonSerializer.Serialize(run.Tags)) : "",
                EscapeCsv(run.Input),
                EscapeCsv(run.SystemPrompt ?? ""),
                EscapeCsv(run.Output ?? ""),
                EscapeCsv(run.Error ?? ""),
                run.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
            };

            IEnumerable<string> metricFields = metricKeys.Select(key =>
                run.Metrics.TryGetValue(key, out double value)
                    ? value.ToString("R", CultureInfo.InvariantCulture)
                    : "");

            sb.AppendLine(string.Join(",", fields.Concat(metricFields)));
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

    private static ExportResult ExportMlflow(List<Run> runs, Experiment experiment)
    {
        var document = new Dictionary<string, object?>
        {
            ["format"] = "mlflow-runs/1",
            ["experiment"] = new Dictionary<string, object?>
            {
                ["experiment_id"] = experiment.Id.ToString(),
                ["name"] = experiment.Name,
            },
            ["runs"] = runs.Select(MlflowRun).ToList(),
        };

        byte[] data = JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        return new ExportResult(
            data,
            "application/json",
            $"experiment-{experiment.Id}-runs.mlflow.json");
    }

    /// <summary>
    /// One run in <c>mlflow.entities.Run.to_dictionary()</c> shape. Params are strings,
    /// metrics doubles; anything Prism did not measure is omitted, because an absent MLflow
    /// metric is queryable as absent while a zero is a lie with units.
    /// </summary>
    /// <param name="run">The run to map.</param>
    /// <returns>The MLflow-shaped dictionary.</returns>
    internal static Dictionary<string, object?> MlflowRun(Run run)
    {
        long startMs = new DateTimeOffset(DateTime.SpecifyKind(run.CreatedAt, DateTimeKind.Utc))
            .ToUnixTimeMilliseconds();

        var parameters = new Dictionary<string, string>();
        RunParameters p = run.Parameters;

        void AddParam(string key, object? value, string? formatted = null)
        {
            if (value is not null)
            {
                parameters[key] = formatted ?? Convert.ToString(value, CultureInfo.InvariantCulture)!;
            }
        }

        AddParam("temperature", p.Temperature, p.Temperature?.ToString("R", CultureInfo.InvariantCulture));
        AddParam("top_p", p.TopP, p.TopP?.ToString("R", CultureInfo.InvariantCulture));
        AddParam("top_k", p.TopK);
        AddParam("max_tokens", p.MaxTokens);
        AddParam("frequency_penalty", p.FrequencyPenalty, p.FrequencyPenalty?.ToString("R", CultureInfo.InvariantCulture));
        AddParam("presence_penalty", p.PresencePenalty, p.PresencePenalty?.ToString("R", CultureInfo.InvariantCulture));
        if (p.StopSequences is { Count: > 0 })
        {
            parameters["stop_sequences"] = JsonSerializer.Serialize(p.StopSequences);
        }

        parameters["model"] = run.Model;

        var metrics = new Dictionary<string, double>
        {
            ["prompt_tokens"] = run.PromptTokens,
            ["completion_tokens"] = run.CompletionTokens,
            ["total_tokens"] = run.TotalTokens,
            ["latency_ms"] = run.LatencyMs,
        };

        if (run.TtftMs is not null)
        {
            metrics["ttft_ms"] = run.TtftMs.Value;
        }

        if (run.TokensPerSecond is not null)
        {
            metrics["tokens_per_second"] = run.TokensPerSecond.Value;
        }

        if (run.Perplexity is not null)
        {
            metrics["perplexity"] = run.Perplexity.Value;
        }

        if (run.Cost is not null)
        {
            metrics["cost_usd"] = (double)run.Cost.Value;
        }

        // Custom metrics ride as-is; they are the values a researcher swept for.
        foreach ((string key, double value) in run.Metrics)
        {
            metrics[key] = value;
        }

        var tags = new Dictionary<string, string>
        {
            ["prism.run_id"] = run.Id.ToString(),
            ["prism.source"] = "prism-experiments",
        };

        if (run.FinishReason is not null)
        {
            tags["prism.finish_reason"] = run.FinishReason;
        }

        if (run.Tags.Count > 0)
        {
            tags["prism.tags"] = string.Join(",", run.Tags);
        }

        if (run.Error is not null)
        {
            tags["prism.error"] = run.Error;
        }

        return new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["run_id"] = run.Id.ToString("N"),
                ["run_name"] = run.Name ?? run.Id.ToString("N")[..8],
                ["experiment_id"] = run.ExperimentId.ToString(),
                ["status"] = MlflowStatus(run.Status),
                ["start_time"] = startMs,
                ["end_time"] = startMs + run.LatencyMs,
                ["user_id"] = "prism",
                ["lifecycle_stage"] = "active",
                ["artifact_uri"] = null,
            },
            ["data"] = new Dictionary<string, object?>
            {
                ["params"] = parameters,
                ["metrics"] = metrics,
                ["tags"] = tags,
            },
        };
    }

    /// <summary>
    /// Maps a Prism run status onto MLflow's <c>RunStatus</c> vocabulary.
    /// </summary>
    /// <param name="status">The Prism status.</param>
    /// <returns>The MLflow status string.</returns>
    internal static string MlflowStatus(RunStatus status) => status switch
    {
        RunStatus.Completed => "FINISHED",
        RunStatus.Failed => "FAILED",
        RunStatus.Running => "RUNNING",
        RunStatus.Pending => "SCHEDULED",
        _ => "KILLED",
    };

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

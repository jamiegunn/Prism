using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Datasets.Domain;
using Prism.Features.FineTuning.Domain;

namespace Prism.Features.FineTuning.Application.ExportDataset;

/// <summary>
/// Command to export a dataset in a fine-tuning format.
/// </summary>
public sealed record ExportFineTuneCommand(
    Guid DatasetId,
    FineTuneExportFormat Format,
    string? InstructionColumn,
    string? InputColumn,
    string? OutputColumn);

/// <summary>
/// Result of a fine-tuning dataset export.
/// </summary>
public sealed record ExportFineTuneResult(
    string Content,
    string ContentType,
    string Filename,
    int RecordCount,
    List<string> Warnings);

/// <summary>
/// Handles exporting datasets in fine-tuning formats (Alpaca, ShareGPT, ChatML, OpenAI JSONL).
/// </summary>
public sealed class ExportFineTuneHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<ExportFineTuneHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportFineTuneHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ExportFineTuneHandler(AppDbContext db, ILogger<ExportFineTuneHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Exports a dataset in the specified fine-tuning format.
    /// </summary>
    /// <param name="command">The export command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The export result with content and metadata.</returns>
    public async Task<Result<ExportFineTuneResult>> HandleAsync(ExportFineTuneCommand command, CancellationToken ct)
    {
        Dataset? dataset = await _db.Set<Dataset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == command.DatasetId, ct);

        if (dataset is null)
            return Error.NotFound($"Dataset {command.DatasetId} not found.");

        List<DatasetRecord> records = await _db.Set<DatasetRecord>()
            .AsNoTracking()
            .Where(r => r.DatasetId == command.DatasetId)
            .OrderBy(r => r.OrderIndex)
            .ToListAsync(ct);

        if (records.Count == 0)
            return Error.Validation("Dataset has no records to export.");

        string instructionCol = command.InstructionColumn ?? "instruction";
        string inputCol = command.InputColumn ?? "input";
        string outputCol = command.OutputColumn ?? "output";

        var warnings = new List<string>();

        return command.Format switch
        {
            FineTuneExportFormat.Alpaca => ExportAlpaca(records, dataset.Name, instructionCol, inputCol, outputCol, warnings),
            FineTuneExportFormat.ShareGpt => ExportShareGpt(records, dataset.Name, instructionCol, inputCol, outputCol, warnings),
            FineTuneExportFormat.ChatMl => ExportChatMl(records, dataset.Name, instructionCol, inputCol, outputCol, warnings),
            FineTuneExportFormat.OpenAiJsonl => ExportOpenAiJsonl(records, dataset.Name, instructionCol, inputCol, outputCol, warnings),
            _ => Error.Validation($"Unknown export format: {command.Format}")
        };
    }

    private static string? GetString(Dictionary<string, object?> data, string key)
    {
        return data.TryGetValue(key, out object? value) ? value?.ToString() : null;
    }

    private static Result<ExportFineTuneResult> ExportAlpaca(
        List<DatasetRecord> records, string datasetName, string instructionCol, string inputCol, string outputCol, List<string> warnings)
    {
        var items = new List<object>();

        for (int i = 0; i < records.Count; i++)
        {
            Dictionary<string, object?> data = records[i].Data;
            string? instruction = GetString(data, instructionCol);

            if (string.IsNullOrWhiteSpace(instruction))
            {
                warnings.Add($"Record {i + 1}: missing '{instructionCol}' field, skipped.");
                continue;
            }

            string? input = GetString(data, inputCol);
            string? output = GetString(data, outputCol);

            if (string.IsNullOrWhiteSpace(output))
            {
                warnings.Add($"Record {i + 1}: missing '{outputCol}' field.");
            }

            items.Add(new { instruction, input = input ?? "", output = output ?? "" });
        }

        string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        return new ExportFineTuneResult(json, "application/json", $"{datasetName}_alpaca.json", items.Count, warnings);
    }

    private static Result<ExportFineTuneResult> ExportShareGpt(
        List<DatasetRecord> records, string datasetName, string instructionCol, string inputCol, string outputCol, List<string> warnings)
    {
        var conversations = new List<object>();

        for (int i = 0; i < records.Count; i++)
        {
            Dictionary<string, object?> data = records[i].Data;
            string? instruction = GetString(data, instructionCol);
            string? input = GetString(data, inputCol);
            string? output = GetString(data, outputCol);

            string userMsg = !string.IsNullOrWhiteSpace(input) ? $"{instruction}\n{input}" : instruction ?? "";

            if (string.IsNullOrWhiteSpace(userMsg))
            {
                warnings.Add($"Record {i + 1}: no instruction or input, skipped.");
                continue;
            }

            var conv = new
            {
                conversations = new[]
                {
                    new { from = "human", value = userMsg },
                    new { from = "gpt", value = output ?? "" }
                }
            };

            conversations.Add(conv);
        }

        string json = JsonSerializer.Serialize(conversations, new JsonSerializerOptions { WriteIndented = true });
        return new ExportFineTuneResult(json, "application/json", $"{datasetName}_sharegpt.json", conversations.Count, warnings);
    }

    private static Result<ExportFineTuneResult> ExportChatMl(
        List<DatasetRecord> records, string datasetName, string instructionCol, string inputCol, string outputCol, List<string> warnings)
    {
        var sb = new System.Text.StringBuilder();
        int count = 0;

        for (int i = 0; i < records.Count; i++)
        {
            Dictionary<string, object?> data = records[i].Data;
            string? instruction = GetString(data, instructionCol);
            string? input = GetString(data, inputCol);
            string? output = GetString(data, outputCol);

            string userMsg = !string.IsNullOrWhiteSpace(input) ? $"{instruction}\n{input}" : instruction ?? "";

            if (string.IsNullOrWhiteSpace(userMsg))
            {
                warnings.Add($"Record {i + 1}: no instruction or input, skipped.");
                continue;
            }

            sb.AppendLine("<|im_start|>user");
            sb.AppendLine(userMsg);
            sb.AppendLine("<|im_end|>");
            sb.AppendLine("<|im_start|>assistant");
            sb.AppendLine(output ?? "");
            sb.AppendLine("<|im_end|>");
            sb.AppendLine();
            count++;
        }

        return new ExportFineTuneResult(sb.ToString(), "text/plain", $"{datasetName}_chatml.txt", count, warnings);
    }

    private static Result<ExportFineTuneResult> ExportOpenAiJsonl(
        List<DatasetRecord> records, string datasetName, string instructionCol, string inputCol, string outputCol, List<string> warnings)
    {
        var sb = new System.Text.StringBuilder();
        int count = 0;

        for (int i = 0; i < records.Count; i++)
        {
            Dictionary<string, object?> data = records[i].Data;
            string? instruction = GetString(data, instructionCol);
            string? input = GetString(data, inputCol);
            string? output = GetString(data, outputCol);

            string userMsg = !string.IsNullOrWhiteSpace(input) ? $"{instruction}\n{input}" : instruction ?? "";

            if (string.IsNullOrWhiteSpace(userMsg))
            {
                warnings.Add($"Record {i + 1}: no instruction or input, skipped.");
                continue;
            }

            var messages = new
            {
                messages = new[]
                {
                    new { role = "user", content = userMsg },
                    new { role = "assistant", content = output ?? "" }
                }
            };

            sb.AppendLine(JsonSerializer.Serialize(messages));
            count++;
        }

        return new ExportFineTuneResult(sb.ToString(), "application/jsonl", $"{datasetName}_openai.jsonl", count, warnings);
    }
}

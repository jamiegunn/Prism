using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Metrics;
using Prism.Common.Inference.Models;
using Prism.Common.Jobs;
using Prism.Common.Results;
using Prism.Features.BatchInference.Domain;
using Prism.Features.Datasets.Domain;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;

namespace Prism.Features.BatchInference.Application.RunBatch;

/// <summary>
/// Executes a batch job: runs every dataset record through the chosen model and persists a
/// result per record.
/// </summary>
/// <remarks>
/// <para>
/// <c>BatchResult</c> previously had no writer anywhere in the codebase — the results, download
/// and retry endpoints all read a table nothing populated. Jobs were created with
/// <c>Status = Queued</c> and no consumer existed.
/// </para>
/// <para>
/// Idempotent, as at-least-once delivery requires: records with a successful result are skipped
/// on a retry, so an interrupted batch resumes instead of re-billing work already paid for.
/// </para>
/// </remarks>
public sealed class BatchJobHandler : IJobHandler
{
    /// <summary>
    /// The <see cref="DurableJob.JobType"/> this handler executes.
    /// </summary>
    public const string Type = "batch-inference";

    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<BatchJobHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchJobHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="providerFactory">Factory for inference providers.</param>
    /// <param name="logger">The logger instance.</param>
    public BatchJobHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<BatchJobHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string JobType => Type;

    /// <inheritdoc />
    public async Task ExecuteAsync(DurableJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        Guid batchJobId = ReadBatchJobId(job);

        BatchJob? batch = await _db.Set<BatchJob>().FirstOrDefaultAsync(b => b.Id == batchJobId, ct);

        if (batch is null)
        {
            _logger.LogWarning("Batch job {BatchJobId} no longer exists; skipping", batchJobId);
            return;
        }

        // Paused counts as a terminal state for this attempt. Forcing Running here would
        // silently overwrite a pause the user had already requested, and the run would carry
        // on regardless — the control would appear to work while doing nothing.
        if (batch.Status is BatchJobStatus.Cancelled or BatchJobStatus.Completed or BatchJobStatus.Paused)
        {
            return;
        }

        batch.Status = BatchJobStatus.Running;
        batch.StartedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        List<DatasetRecord> records = await LoadRecordsAsync(batch, ct);

        HashSet<Guid> alreadySucceeded = (await _db.Set<BatchResult>()
                .AsNoTracking()
                .Where(r => r.BatchJobId == batchJobId && r.Status == BatchResultStatus.Success)
                .Select(r => r.RecordId)
                .ToListAsync(ct))
            .ToHashSet();

        // A record being re-run (retry-failed, or a resumed run that died mid-record) already
        // has a non-success row. Reuse it — adding a fresh row per attempt would show a
        // six-record dataset as twelve results after one retry.
        Dictionary<Guid, BatchResult> reusableResults = (await _db.Set<BatchResult>()
                .Where(r => r.BatchJobId == batchJobId && r.Status != BatchResultStatus.Success)
                .ToListAsync(ct))
            .GroupBy(r => r.RecordId)
            .ToDictionary(g => g.Key, g => g.First());

        // Default first, then online — an unordered FirstOrDefault here is the documented
        // arbitrary-row trap: it ran whole batches against a dead seeded endpoint while a
        // healthy default instance sat idle (the evaluation runner had the same bug).
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .OrderByDescending(i => i.IsDefault)
            .ThenByDescending(i => i.Status == InstanceStatus.Online)
            .FirstOrDefaultAsync(ct);

        if (instance is null)
        {
            throw new InvalidOperationException(
                "No inference instance is registered, so there is nothing to run the batch against.");
        }

        // The batch records the model it was created with; the instance is the fallback for a
        // batch that never named one, so a blank never reaches the server as `model is required`.
        Result<string> batchModelResult = ModelSelection.Resolve(instance, batch.Model);
        if (batchModelResult.IsFailure)
        {
            throw new InvalidOperationException(batchModelResult.Error.Message);
        }

        string batchModel = batchModelResult.Value;

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        foreach (DatasetRecord record in records)
        {
            ct.ThrowIfCancellationRequested();

            if (alreadySucceeded.Contains(record.Id))
            {
                continue;
            }

            // Pause and cancel are only meaningful if the running job notices them. Re-reading
            // the status each record is what makes the buttons do anything.
            BatchJobStatus current = await CurrentStatusAsync(batchJobId, ct);

            if (current is BatchJobStatus.Paused)
            {
                _logger.LogInformation("Batch job {BatchJobId} paused; stopping after {Done} records",
                    batchJobId, batch.CompletedRecords + batch.FailedRecords);
                return;
            }

            if (current is BatchJobStatus.Cancelled)
            {
                _logger.LogInformation("Batch job {BatchJobId} cancelled", batchJobId);
                return;
            }

            BatchResult result = await RunRecordAsync(batch, batchModel, record, provider, ct);

            if (reusableResults.TryGetValue(record.Id, out BatchResult? prior))
            {
                prior.Status = result.Status;
                prior.Output = result.Output;
                prior.Error = result.Error;
                prior.LatencyMs = result.LatencyMs;
                prior.TokensUsed = result.TokensUsed;
                prior.LogprobsData = result.LogprobsData;
                prior.Perplexity = result.Perplexity;
                prior.Attempt++;
                result = prior;
            }
            else
            {
                _db.Set<BatchResult>().Add(result);
            }

            if (result.Status == BatchResultStatus.Success)
            {
                batch.CompletedRecords++;
                batch.TokensUsed += result.TokensUsed;
            }
            else
            {
                batch.FailedRecords++;
            }

            batch.Progress = batch.TotalRecords == 0
                ? 100
                : Math.Round(
                    100.0 * (batch.CompletedRecords + batch.FailedRecords) / batch.TotalRecords, 2);

            await _db.SaveChangesAsync(ct);
        }

        batch.Status = BatchJobStatus.Completed;
        batch.FinishedAt = DateTime.UtcNow;
        batch.Progress = 100;

        // Null rather than zero for local models: an unknown cost and a cost of nothing are
        // different claims, and only one of them is true.
        batch.Cost = CostCalculator.HasPricing(batch.Model)
            ? CostCalculator.EstimateCost(batch.Model, (int)batch.TokensUsed, 0)
            : null;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Batch job {BatchJobId} finished: {Completed} succeeded, {Failed} failed, {Tokens} tokens",
            batch.Id, batch.CompletedRecords, batch.FailedRecords, batch.TokensUsed);
    }

    /// <summary>
    /// Extracts the batch job identifier from a durable job's parameters.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns>The batch job identifier.</returns>
    /// <exception cref="InvalidOperationException">The parameters do not name a batch job.</exception>
    internal static Guid ReadBatchJobId(DurableJob job)
    {
        using JsonDocument document = JsonDocument.Parse(job.ParametersJson);

        if (!document.RootElement.TryGetProperty("batchJobId", out JsonElement element)
            || !element.TryGetGuid(out Guid id))
        {
            throw new InvalidOperationException(
                $"Job {job.Id} has no 'batchJobId' parameter, so there is nothing to run.");
        }

        return id;
    }

    /// <summary>
    /// Estimates the token count of a piece of text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>An approximate token count.</returns>
    /// <remarks>
    /// Roughly four characters per token, the usual approximation for English BPE vocabularies.
    /// Crude, but derived from the actual content — unlike the flat 500-tokens-per-record
    /// constant it replaces, which ignored the data and the model alike.
    /// </remarks>
    internal static int EstimateTokens(string text)
        => string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);

    private async Task<BatchJobStatus> CurrentStatusAsync(Guid batchJobId, CancellationToken ct)
        => await _db.Set<BatchJob>()
            .AsNoTracking()
            .Where(b => b.Id == batchJobId)
            .Select(b => b.Status)
            .FirstAsync(ct);

    private async Task<BatchResult> RunRecordAsync(
        BatchJob batch,
        string batchModel,
        DatasetRecord record,
        IInferenceProvider provider,
        CancellationToken ct)
    {
        string input = ExtractInput(record);

        var result = new BatchResult
        {
            BatchJobId = batch.Id,
            RecordId = record.Id,
            Input = input,
            Attempt = 1,
        };

        var stopwatch = Stopwatch.StartNew();

        Result<ChatResponse> response = await provider.ChatAsync(
            new ChatRequest
            {
                Model = batchModel,
                Messages = [ChatMessage.User(input)],
                Logprobs = batch.CaptureLogprobs,
                SourceModule = "batch-inference",
            },
            ct);

        stopwatch.Stop();
        result.LatencyMs = stopwatch.ElapsedMilliseconds;

        if (response.IsFailure)
        {
            result.Status = BatchResultStatus.Failed;
            result.Error = response.Error.Message;
            return result;
        }

        result.Status = BatchResultStatus.Success;
        result.Output = response.Value.Content;
        result.TokensUsed = response.Value.Usage?.TotalTokens ?? 0;

        if (batch.CaptureLogprobs && response.Value.LogprobsData is { Tokens.Count: > 0 } logprobs)
        {
            result.LogprobsData = JsonSerializer.Serialize(logprobs);
            result.Perplexity = LogprobsCalculator.CalculatePerplexity(logprobs);
        }

        return result;
    }

    private static string ExtractInput(DatasetRecord record)
    {
        foreach (string key in new[] { "input", "prompt", "question", "instruction", "text" })
        {
            if (record.Data.TryGetValue(key, out object? value) && value is not null)
            {
                string text = value is JsonElement json
                    ? json.ValueKind == JsonValueKind.String ? json.GetString() ?? "" : json.ToString()
                    : value.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private async Task<List<DatasetRecord>> LoadRecordsAsync(BatchJob batch, CancellationToken ct)
    {
        IQueryable<DatasetRecord> query = _db.Set<DatasetRecord>()
            .AsNoTracking()
            .Where(r => r.DatasetId == batch.DatasetId);

        if (!string.IsNullOrWhiteSpace(batch.SplitLabel))
        {
            query = query.Where(r => r.SplitLabel == batch.SplitLabel);
        }

        return await query.OrderBy(r => r.OrderIndex).ToListAsync(ct);
    }
}

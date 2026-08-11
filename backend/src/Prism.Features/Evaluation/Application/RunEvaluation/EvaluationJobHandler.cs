using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Jobs;
using Prism.Common.Results;
using Prism.Features.Datasets.Domain;
using Prism.Features.Evaluation.Domain;
using Prism.Features.Evaluation.Domain.Scorers;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;

namespace Prism.Features.Evaluation.Application.RunEvaluation;

/// <summary>
/// Executes an evaluation: runs every dataset record against every model under test, scores the
/// outputs, and persists a row per record per model.
/// </summary>
/// <remarks>
/// <para>
/// This is the piece that was missing. <c>StartEvaluationHandler</c> documented itself as
/// enqueuing work for background processing but only inserted a row with <c>Status = Pending</c>.
/// Nothing consumed it, so <c>EvaluationResult</c> was written by zero lines of code and the
/// results, leaderboard and export endpoints were permanently empty. The five scorers were
/// correctly implemented, registered in DI, and never called by anything.
/// </para>
/// <para>
/// Idempotent as <see cref="IJobHandler"/> requires: results already recorded for a
/// (record, model) pair are skipped, so a retry after a partial run resumes rather than
/// duplicating.
/// </para>
/// </remarks>
public sealed class EvaluationJobHandler : IJobHandler
{
    /// <summary>
    /// The <see cref="DurableJob.JobType"/> this handler executes.
    /// </summary>
    public const string Type = "evaluation";

    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly IEnumerable<IScoringMethod> _scorers;
    private readonly ILogger<EvaluationJobHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationJobHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="providerFactory">Factory for inference providers.</param>
    /// <param name="scorers">All registered scoring methods.</param>
    /// <param name="logger">The logger instance.</param>
    public EvaluationJobHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        IEnumerable<IScoringMethod> scorers,
        ILogger<EvaluationJobHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _scorers = scorers;
        _logger = logger;
    }

    /// <inheritdoc />
    public string JobType => Type;

    /// <inheritdoc />
    public async Task ExecuteAsync(DurableJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        Guid evaluationId = ReadEvaluationId(job);

        EvaluationEntity? evaluation = await _db.Set<EvaluationEntity>()
            .FirstOrDefaultAsync(e => e.Id == evaluationId, ct);

        if (evaluation is null)
        {
            // The evaluation was deleted after the job was queued. Nothing to do, and failing
            // would retry a job that can never succeed.
            _logger.LogWarning("Evaluation {EvaluationId} no longer exists; skipping", evaluationId);
            return;
        }

        evaluation.Status = EvaluationStatus.Running;
        evaluation.StartedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        List<DatasetRecord> records = await LoadRecordsAsync(evaluation, ct);

        // Resuming a retried job: skip pairs already scored.
        HashSet<(Guid RecordId, string Model)> alreadyDone = (await _db.Set<EvaluationResult>()
                .AsNoTracking()
                .Where(r => r.EvaluationId == evaluationId)
                .Select(r => new { r.RecordId, r.Model })
                .ToListAsync(ct))
            .Select(x => (x.RecordId, x.Model))
            .ToHashSet();

        // The default instance, not an arbitrary one: picking whatever row came first is
        // how a run ended up on a dead seeded endpoint while the healthy default sat idle.
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .OrderByDescending(i => i.IsDefault)
            .ThenByDescending(i => i.Status == InstanceStatus.Online)
            .FirstOrDefaultAsync(ct);

        if (instance is null)
        {
            throw new InvalidOperationException(
                "No inference instance is registered, so there is nothing to evaluate against.");
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        foreach (string model in evaluation.Models)
        {
            // Resolved per model because llm_judge needs a judge model; the definitions each
            // scorer reports are recorded on the evaluation so every number stays citable
            // after the implementation moves on.
            Dictionary<string, IScoringMethod> scorers = ResolveScorers(evaluation, provider, model);

            foreach ((string name, IScoringMethod scorer) in scorers)
            {
                evaluation.ScoreDefinitions[name] = scorer.Definition;
            }

            await _db.SaveChangesAsync(ct);

            foreach (DatasetRecord record in records)
            {
                ct.ThrowIfCancellationRequested();

                if (alreadyDone.Contains((record.Id, model)))
                {
                    continue;
                }

                EvaluationResult result = await EvaluateRecordAsync(
                    evaluation, record, model, provider, scorers, ct);

                _db.Set<EvaluationResult>().Add(result);

                if (result.Error is null)
                {
                    evaluation.CompletedRecords++;
                }
                else
                {
                    evaluation.FailedRecords++;
                }

                evaluation.Progress = evaluation.TotalRecords == 0
                    ? 100
                    : Math.Round(
                        100.0 * (evaluation.CompletedRecords + evaluation.FailedRecords)
                        / evaluation.TotalRecords, 2);

                await _db.SaveChangesAsync(ct);
            }
        }

        evaluation.Status = EvaluationStatus.Completed;
        evaluation.FinishedAt = DateTime.UtcNow;
        evaluation.Progress = 100;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Evaluation {EvaluationId} finished: {Completed} scored, {Failed} failed",
            evaluation.Id, evaluation.CompletedRecords, evaluation.FailedRecords);
    }

    /// <summary>
    /// Extracts the evaluation identifier from a job's parameters.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns>The evaluation identifier.</returns>
    /// <exception cref="InvalidOperationException">The parameters do not name an evaluation.</exception>
    internal static Guid ReadEvaluationId(DurableJob job)
    {
        using JsonDocument document = JsonDocument.Parse(job.ParametersJson);

        if (!document.RootElement.TryGetProperty("evaluationId", out JsonElement element)
            || !element.TryGetGuid(out Guid id))
        {
            throw new InvalidOperationException(
                $"Job {job.Id} has no 'evaluationId' parameter, so there is nothing to evaluate.");
        }

        return id;
    }

    /// <summary>
    /// Pulls the text to send to the model and the reference answer out of a dataset record.
    /// </summary>
    /// <param name="record">The dataset record.</param>
    /// <returns>The prompt and the expected output, either of which may be empty.</returns>
    /// <remarks>
    /// Dataset records are schemaless JSON. Rather than demand one shape, this accepts the
    /// field names in common use across instruction datasets.
    /// </remarks>
    internal static (string Input, string? Expected) ExtractFields(DatasetRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string? input = FirstNonEmpty(record.Data, "input", "prompt", "question", "instruction", "text");
        string? expected = FirstNonEmpty(record.Data, "expected", "output", "answer", "completion", "target", "reference", "label");

        return (input ?? string.Empty, expected);
    }

    private static string? FirstNonEmpty(Dictionary<string, object?> data, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (data.TryGetValue(key, out object? value) && value is not null)
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

        return null;
    }

    private async Task<EvaluationResult> EvaluateRecordAsync(
        EvaluationEntity evaluation,
        DatasetRecord record,
        string model,
        IInferenceProvider provider,
        Dictionary<string, IScoringMethod> scorers,
        CancellationToken ct)
    {
        (string input, string? expected) = ExtractFields(record);

        var result = new EvaluationResult
        {
            EvaluationId = evaluation.Id,
            RecordId = record.Id,
            Model = model,
            Input = input,
            ExpectedOutput = expected,
        };

        var stopwatch = Stopwatch.StartNew();

        // Logprobs are requested when the provider claims support: they cost nothing to
        // store and are what calibration (ECE, Brier) is computed from later. Whatever comes
        // back is stored even if the capability flag was wrong — flags here have lied before.
        bool requestLogprobs = provider.Capabilities.SupportsLogprobs;

        Result<ChatResponse> response = await provider.ChatAsync(
            new ChatRequest
            {
                Model = model,
                Messages = [ChatMessage.User(input)],
                Logprobs = requestLogprobs,
                TopLogprobs = requestLogprobs ? 5 : null,
                SourceModule = "evaluation",
            },
            ct);

        stopwatch.Stop();
        result.LatencyMs = stopwatch.ElapsedMilliseconds;

        if (response.IsFailure)
        {
            // One bad record must not abort the run. Recording the failure keeps it visible
            // and countable rather than silently shrinking the denominator.
            result.Error = response.Error.Message;
            return result;
        }

        result.ActualOutput = response.Value.Content;
        result.PromptTokens = response.Value.Usage?.PromptTokens ?? 0;
        result.CompletionTokens = response.Value.Usage?.CompletionTokens ?? 0;

        if (response.Value.LogprobsData is { Tokens.Count: > 0 } logprobs)
        {
            result.LogprobsData = JsonSerializer.Serialize(logprobs);
            result.Perplexity = Prism.Common.Inference.Metrics.LogprobsCalculator
                .CalculatePerplexity(logprobs);
        }

        foreach ((string name, IScoringMethod scorer) in scorers)
        {
            try
            {
                result.Scores[name] = await scorer.ScoreAsync(
                    input, expected ?? string.Empty, result.ActualOutput ?? string.Empty, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex, "Scorer {Scorer} failed for record {RecordId}", name, record.Id);
            }
        }

        return result;
    }

    private async Task<List<DatasetRecord>> LoadRecordsAsync(
        EvaluationEntity evaluation, CancellationToken ct)
    {
        IQueryable<DatasetRecord> query = _db.Set<DatasetRecord>()
            .AsNoTracking()
            .Where(r => r.DatasetId == evaluation.DatasetId);

        if (!string.IsNullOrWhiteSpace(evaluation.SplitLabel))
        {
            query = query.Where(r => r.SplitLabel == evaluation.SplitLabel);
        }

        return await query.OrderBy(r => r.OrderIndex).ToListAsync(ct);
    }

    private Dictionary<string, IScoringMethod> ResolveScorers(
        EvaluationEntity evaluation, IInferenceProvider provider, string model)
    {
        Dictionary<string, IScoringMethod> available =
            _scorers.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        var resolved = new Dictionary<string, IScoringMethod>(StringComparer.OrdinalIgnoreCase);

        foreach (string requested in evaluation.ScoringMethods)
        {
            if (available.TryGetValue(requested, out IScoringMethod? scorer))
            {
                resolved[scorer.Name] = scorer;
            }
            else if (string.Equals(requested, "llm_judge", StringComparison.OrdinalIgnoreCase))
            {
                // The judge cannot live in DI: it needs a provider and a judge model, both
                // chosen per run. Constructed here, judging with the model under test.
                resolved["llm_judge"] = new LlmJudgeScorer(provider, model);
            }
            else
            {
                // Naming a scorer that does not exist is a configuration mistake worth
                // surfacing, but not worth discarding the rest of the run over.
                _logger.LogWarning(
                    "Evaluation {EvaluationId} requested unknown scoring method '{Method}'",
                    evaluation.Id, requested);
            }
        }

        return resolved;
    }
}

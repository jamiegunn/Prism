using Prism.Features.BatchInference.Application.RunBatch;
using Prism.Common.Inference.Metrics;
using Microsoft.EntityFrameworkCore;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.BatchInference.Application.EstimateBatchCost;

/// <summary>
/// Command to estimate the cost and time for a batch inference job.
/// </summary>
public sealed record EstimateBatchCostCommand(Guid DatasetId, string? SplitLabel, string Model, int Concurrency);

/// <summary>
/// Handles estimating batch job costs.
/// </summary>
public sealed class EstimateBatchCostHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="EstimateBatchCostHandler"/> class.
    /// </summary>
    public EstimateBatchCostHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the estimate batch cost command.
    /// </summary>
    public async Task<Result<BatchEstimateDto>> HandleAsync(EstimateBatchCostCommand command, CancellationToken ct)
    {
        IQueryable<DatasetRecord> recordsQuery = _db.Set<DatasetRecord>()
            .Where(r => r.DatasetId == command.DatasetId);

        if (!string.IsNullOrWhiteSpace(command.SplitLabel))
        {
            recordsQuery = recordsQuery.Where(r => r.SplitLabel == command.SplitLabel);
        }

        int recordCount = await recordsQuery.CountAsync(ct);
        if (recordCount == 0)
        {
            return Error.Validation("No records found in the dataset for the specified split.");
        }

        // Estimate from the actual prompts rather than a flat per-record constant. The previous
        // version multiplied the record count by 500 tokens, ignoring both the data and the
        // model, and returned no cost at all despite the endpoint being named for one.
        List<DatasetRecord> sample = await recordsQuery
            .AsNoTracking()
            .OrderBy(r => r.OrderIndex)
            .Take(SampleSize)
            .ToListAsync(ct);

        double meanPromptTokens = sample.Count == 0
            ? 0
            : sample.Average(r => BatchJobHandler.EstimateTokens(ExtractInput(r)));

        // Completions are unknown before the run. Assume a response of similar length to the
        // prompt — wrong in either direction for some workloads, but honest about being an
        // estimate and at least proportional to the real input.
        int estimatedTokens = (int)Math.Ceiling(meanPromptTokens * 2 * recordCount);

        int concurrency = Math.Max(1, command.Concurrency);
        double estimatedMinutes = (double)recordCount / concurrency / 60.0;

        decimal? estimatedCost = CostCalculator.HasPricing(command.Model)
            ? CostCalculator.EstimateCost(command.Model, estimatedTokens / 2, estimatedTokens / 2)
            : null;

        return new BatchEstimateDto(
            recordCount,
            estimatedTokens,
            Math.Round(estimatedMinutes, 1),
            command.Model,
            estimatedCost);
    }

    // Sampling rather than reading the whole dataset: an estimate that costs as much as the run
    // is not an estimate.
    private const int SampleSize = 50;

    private static string ExtractInput(DatasetRecord record)
    {
        foreach (string key in new[] { "input", "prompt", "question", "instruction", "text" })
        {
            if (record.Data.TryGetValue(key, out object? value) && value is not null)
            {
                string text = value.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }
}

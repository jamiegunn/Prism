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

        // Rough estimate: ~500 tokens per record (input + output), ~1 second per request
        int estimatedTokens = recordCount * 500;
        int concurrency = Math.Max(1, command.Concurrency);
        double estimatedMinutes = (double)recordCount / concurrency / 60.0;

        return new BatchEstimateDto(recordCount, estimatedTokens, Math.Round(estimatedMinutes, 1), command.Model);
    }
}

using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.CompareRuns;

/// <summary>
/// Handles comparison of multiple runs, highlighting parameter diffs and metric deltas.
/// </summary>
public sealed class CompareRunsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompareRunsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public CompareRunsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Compares the specified runs, producing parameter diffs and metric comparisons.
    /// </summary>
    /// <param name="query">The query containing the experiment and run IDs.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the comparison DTO on success.</returns>
    public async Task<Result<RunComparisonDto>> HandleAsync(CompareRunsQuery query, CancellationToken ct)
    {
        List<Run> runs = await _db.Set<Run>()
            .AsNoTracking()
            .Where(r => r.ExperimentId == query.ExperimentId && query.RunIds.Contains(r.Id))
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        if (runs.Count < 2)
        {
            return Error.Validation("At least 2 runs are required for comparison.");
        }

        List<RunDto> runDtos = runs.Select(RunDto.FromEntity).ToList();

        // Build parameter diffs
        var paramDiffs = new Dictionary<string, List<string?>>
        {
            ["model"] = runs.Select(r => (string?)r.Model).ToList(),
            ["temperature"] = runs.Select(r => r.Parameters.Temperature?.ToString()).ToList(),
            ["topP"] = runs.Select(r => r.Parameters.TopP?.ToString()).ToList(),
            ["topK"] = runs.Select(r => r.Parameters.TopK?.ToString()).ToList(),
            ["maxTokens"] = runs.Select(r => r.Parameters.MaxTokens?.ToString()).ToList(),
            ["frequencyPenalty"] = runs.Select(r => r.Parameters.FrequencyPenalty?.ToString()).ToList(),
            ["presencePenalty"] = runs.Select(r => r.Parameters.PresencePenalty?.ToString()).ToList()
        };

        // Build metric comparison — include built-in metrics + custom metrics
        var metricComparison = new Dictionary<string, List<double?>>
        {
            ["latencyMs"] = runs.Select(r => (double?)r.LatencyMs).ToList(),
            ["promptTokens"] = runs.Select(r => (double?)r.PromptTokens).ToList(),
            ["completionTokens"] = runs.Select(r => (double?)r.CompletionTokens).ToList(),
            ["totalTokens"] = runs.Select(r => (double?)r.TotalTokens).ToList(),
            ["tokensPerSecond"] = runs.Select(r => r.TokensPerSecond).ToList(),
            ["perplexity"] = runs.Select(r => r.Perplexity).ToList(),
            ["cost"] = runs.Select(r => r.Cost.HasValue ? (double?)((double)r.Cost.Value) : null).ToList()
        };

        // Add custom metrics from all runs
        HashSet<string> allMetricKeys = runs.SelectMany(r => r.Metrics.Keys).ToHashSet();
        foreach (string key in allMetricKeys)
        {
            metricComparison[key] = runs.Select(r => r.Metrics.TryGetValue(key, out double v) ? (double?)v : null).ToList();
        }

        return new RunComparisonDto(runDtos, paramDiffs, metricComparison);
    }
}

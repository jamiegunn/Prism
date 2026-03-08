using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.GetDatasetStats;

/// <summary>
/// Query to retrieve statistics for a dataset.
/// </summary>
/// <param name="DatasetId">The dataset identifier.</param>
public sealed record GetDatasetStatsQuery(Guid DatasetId);

/// <summary>
/// Handles computing dataset statistics.
/// </summary>
public sealed class GetDatasetStatsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDatasetStatsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetDatasetStatsHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Computes statistics for a dataset including split distribution and per-column stats.
    /// </summary>
    /// <param name="query">The stats query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the dataset statistics.</returns>
    public async Task<Result<DatasetStatsDto>> HandleAsync(GetDatasetStatsQuery query, CancellationToken ct)
    {
        Dataset? dataset = await _db.Set<Dataset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == query.DatasetId, ct);

        if (dataset is null)
        {
            return Error.NotFound($"Dataset {query.DatasetId} not found.");
        }

        List<DatasetRecord> records = await _db.Set<DatasetRecord>()
            .AsNoTracking()
            .Where(r => r.DatasetId == query.DatasetId)
            .ToListAsync(ct);

        // Split distribution
        var splitDistribution = records
            .GroupBy(r => r.SplitLabel ?? "unassigned")
            .ToDictionary(g => g.Key, g => g.Count());

        // Per-column statistics
        var columnStats = new List<ColumnStatsDto>();
        foreach (ColumnSchema col in dataset.Schema)
        {
            var values = new List<string>();
            int nullCount = 0;

            foreach (DatasetRecord record in records)
            {
                if (record.Data.TryGetValue(col.Name, out object? val) && val is not null)
                {
                    string stringVal = val is JsonElement je ? je.ToString() : val.ToString() ?? "";
                    values.Add(stringVal);
                }
                else
                {
                    nullCount++;
                }
            }

            var topValues = values
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count());

            columnStats.Add(new ColumnStatsDto(
                col.Name,
                values.Count,
                nullCount,
                values.Distinct().Count(),
                topValues));
        }

        return new DatasetStatsDto(records.Count, splitDistribution, columnStats);
    }
}

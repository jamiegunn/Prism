using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.SplitDataset;

/// <summary>
/// Command to split a dataset into train/test/val partitions.
/// </summary>
/// <param name="DatasetId">The dataset identifier.</param>
/// <param name="TrainRatio">The proportion for training (0-1).</param>
/// <param name="TestRatio">The proportion for testing (0-1).</param>
/// <param name="ValRatio">The proportion for validation (0-1).</param>
/// <param name="Seed">Optional random seed for reproducibility.</param>
public sealed record SplitDatasetCommand(Guid DatasetId, double TrainRatio, double TestRatio, double ValRatio, int? Seed);

/// <summary>
/// Handles splitting a dataset into partitions.
/// </summary>
public sealed class SplitDatasetHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplitDatasetHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public SplitDatasetHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Splits dataset records into train/test/val partitions.
    /// </summary>
    /// <param name="command">The split command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated dataset DTO.</returns>
    public async Task<Result<DatasetDto>> HandleAsync(SplitDatasetCommand command, CancellationToken ct)
    {
        double total = command.TrainRatio + command.TestRatio + command.ValRatio;
        if (Math.Abs(total - 1.0) > 0.01)
        {
            return Error.Validation("Split ratios must sum to 1.0.");
        }

        Dataset? dataset = await _db.Set<Dataset>()
            .Include(d => d.Splits)
            .FirstOrDefaultAsync(d => d.Id == command.DatasetId, ct);

        if (dataset is null)
        {
            return Error.NotFound($"Dataset {command.DatasetId} not found.");
        }

        List<DatasetRecord> records = await _db.Set<DatasetRecord>()
            .Where(r => r.DatasetId == command.DatasetId)
            .OrderBy(r => r.OrderIndex)
            .ToListAsync(ct);

        // Shuffle for random split
        var rng = command.Seed.HasValue ? new Random(command.Seed.Value) : new Random();
        List<DatasetRecord> shuffled = records.OrderBy(_ => rng.Next()).ToList();

        int trainCount = (int)Math.Round(shuffled.Count * command.TrainRatio);
        int testCount = (int)Math.Round(shuffled.Count * command.TestRatio);

        // Assign split labels
        for (int i = 0; i < shuffled.Count; i++)
        {
            if (i < trainCount)
                shuffled[i].SplitLabel = "train";
            else if (i < trainCount + testCount)
                shuffled[i].SplitLabel = "test";
            else
                shuffled[i].SplitLabel = "val";
        }

        // Remove old splits and create new ones
        _db.Set<DatasetSplit>().RemoveRange(dataset.Splits);

        int actualTrain = shuffled.Count(r => r.SplitLabel == "train");
        int actualTest = shuffled.Count(r => r.SplitLabel == "test");
        int actualVal = shuffled.Count(r => r.SplitLabel == "val");

        dataset.Splits =
        [
            new DatasetSplit { DatasetId = dataset.Id, Name = "train", RecordCount = actualTrain },
            new DatasetSplit { DatasetId = dataset.Id, Name = "test", RecordCount = actualTest },
            new DatasetSplit { DatasetId = dataset.Id, Name = "val", RecordCount = actualVal },
        ];

        dataset.Version++;
        dataset.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return DatasetDto.FromEntity(dataset);
    }
}

using Microsoft.EntityFrameworkCore;
using Prism.Common.Database.Seeders;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Infrastructure;

/// <summary>
/// Seeds a sample sentiment analysis dataset with records and train/test/val splits
/// to demonstrate the Datasets feature on first launch.
/// </summary>
public sealed class DatasetsSeeder : IDataSeeder
{
    /// <summary>
    /// Gets the execution order. Datasets seed at order 140, after prompt lab.
    /// </summary>
    public int Order => 140;

    /// <summary>
    /// Seeds a sample dataset with six records and three splits if none exist.
    /// Creates a small sentiment analysis benchmark with train, test, and validation splits.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasDatasets = await context.Set<Dataset>().AnyAsync(ct);

        if (hasDatasets)
        {
            return;
        }

        Guid datasetId = Guid.NewGuid();

        var dataset = new Dataset
        {
            Id = datasetId,
            Name = "Sentiment Analysis Samples",
            Description = "Small benchmark dataset for testing sentiment classification",
            Format = DatasetFormat.Jsonl,
            Version = 1,
            RecordCount = 6,
            SizeBytes = 420,
            Schema =
            [
                new ColumnSchema { Name = "text", Type = "string", Purpose = "input" },
                new ColumnSchema { Name = "label", Type = "string", Purpose = "label" },
                new ColumnSchema { Name = "confidence", Type = "number", Purpose = "metadata" }
            ],
            Records =
            [
                new DatasetRecord
                {
                    DatasetId = datasetId,
                    OrderIndex = 0,
                    SplitLabel = "train",
                    Data = new Dictionary<string, object?>
                    {
                        ["text"] = "This product exceeded my expectations!",
                        ["label"] = "positive",
                        ["confidence"] = 0.95
                    }
                },
                new DatasetRecord
                {
                    DatasetId = datasetId,
                    OrderIndex = 1,
                    SplitLabel = "train",
                    Data = new Dictionary<string, object?>
                    {
                        ["text"] = "Terrible experience, would not recommend.",
                        ["label"] = "negative",
                        ["confidence"] = 0.92
                    }
                },
                new DatasetRecord
                {
                    DatasetId = datasetId,
                    OrderIndex = 2,
                    SplitLabel = "train",
                    Data = new Dictionary<string, object?>
                    {
                        ["text"] = "It's okay, nothing special.",
                        ["label"] = "neutral",
                        ["confidence"] = 0.78
                    }
                },
                new DatasetRecord
                {
                    DatasetId = datasetId,
                    OrderIndex = 3,
                    SplitLabel = "test",
                    Data = new Dictionary<string, object?>
                    {
                        ["text"] = "Absolutely love it! Best purchase ever.",
                        ["label"] = "positive",
                        ["confidence"] = 0.97
                    }
                },
                new DatasetRecord
                {
                    DatasetId = datasetId,
                    OrderIndex = 4,
                    SplitLabel = "test",
                    Data = new Dictionary<string, object?>
                    {
                        ["text"] = "The quality was disappointing.",
                        ["label"] = "negative",
                        ["confidence"] = 0.89
                    }
                },
                new DatasetRecord
                {
                    DatasetId = datasetId,
                    OrderIndex = 5,
                    SplitLabel = "val",
                    Data = new Dictionary<string, object?>
                    {
                        ["text"] = "Works as described, decent value.",
                        ["label"] = "neutral",
                        ["confidence"] = 0.72
                    }
                }
            ],
            Splits =
            [
                new DatasetSplit
                {
                    DatasetId = datasetId,
                    Name = "train",
                    RecordCount = 3
                },
                new DatasetSplit
                {
                    DatasetId = datasetId,
                    Name = "test",
                    RecordCount = 2
                },
                new DatasetSplit
                {
                    DatasetId = datasetId,
                    Name = "val",
                    RecordCount = 1
                }
            ]
        };

        context.Set<Dataset>().Add(dataset);
        await context.SaveChangesAsync(ct);
    }
}

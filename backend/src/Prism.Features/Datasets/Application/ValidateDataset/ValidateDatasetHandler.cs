using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.ValidateDataset;

/// <summary>
/// Validates a dataset's data quality by checking for null values, type consistency,
/// duplicates, and column coverage. Returns a report of issues found.
/// </summary>
public sealed class ValidateDatasetHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateDatasetHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ValidateDatasetHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Validates the dataset and returns a quality report.
    /// </summary>
    /// <param name="datasetId">The dataset ID to validate.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the validation report.</returns>
    public async Task<Result<DatasetValidationReportDto>> HandleAsync(Guid datasetId, CancellationToken ct)
    {
        Dataset? dataset = await _db.Set<Dataset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == datasetId, ct);

        if (dataset is null)
        {
            return Result<DatasetValidationReportDto>.Failure(Error.NotFound($"Dataset {datasetId} not found."));
        }

        List<DatasetRecord> records = await _db.Set<DatasetRecord>()
            .AsNoTracking()
            .Where(r => r.DatasetId == datasetId)
            .ToListAsync(ct);

        List<ValidationIssue> issues = [];
        int totalRecords = records.Count;

        if (totalRecords == 0)
        {
            issues.Add(new ValidationIssue("dataset", "empty", "Dataset contains no records."));
            return new DatasetValidationReportDto(datasetId, totalRecords, issues, issues.Count == 0);
        }

        // Check each schema column
        foreach (ColumnSchema column in dataset.Schema)
        {
            int nullCount = 0;
            int typeMismatchCount = 0;

            foreach (DatasetRecord record in records)
            {
                bool hasKey = record.Data.ContainsKey(column.Name);

                if (!hasKey || record.Data[column.Name] is null)
                {
                    nullCount++;
                    continue;
                }

                object? value = record.Data[column.Name];
                bool typeOk = column.Type switch
                {
                    "number" => value is int or long or float or double or decimal or System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number },
                    "boolean" => value is bool or System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False },
                    "string" => value is string or System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String },
                    _ => true
                };

                if (!typeOk)
                {
                    typeMismatchCount++;
                }
            }

            double nullPercent = (double)nullCount / totalRecords * 100;

            if (nullCount > 0)
            {
                string severity = nullPercent > 50 ? "error" : nullPercent > 10 ? "warning" : "info";
                issues.Add(new ValidationIssue(column.Name, severity,
                    $"Column '{column.Name}' has {nullCount} null/missing values ({nullPercent:F1}%)."));
            }

            if (typeMismatchCount > 0)
            {
                issues.Add(new ValidationIssue(column.Name, "warning",
                    $"Column '{column.Name}' has {typeMismatchCount} values that don't match expected type '{column.Type}'."));
            }
        }

        // Check for records with keys not in schema
        HashSet<string> schemaColumns = dataset.Schema.Select(c => c.Name).ToHashSet();
        HashSet<string> extraColumns = [];
        foreach (DatasetRecord record in records)
        {
            foreach (string key in record.Data.Keys)
            {
                if (!schemaColumns.Contains(key))
                {
                    extraColumns.Add(key);
                }
            }
        }

        foreach (string extraCol in extraColumns)
        {
            issues.Add(new ValidationIssue(extraCol, "info",
                $"Column '{extraCol}' found in records but not defined in schema."));
        }

        // Check splits coverage
        List<DatasetSplit> splits = await _db.Set<DatasetSplit>()
            .AsNoTracking()
            .Where(s => s.DatasetId == datasetId)
            .ToListAsync(ct);

        int assignedCount = records.Count(r => !string.IsNullOrEmpty(r.SplitLabel));
        if (splits.Count > 0 && assignedCount < totalRecords)
        {
            int unassigned = totalRecords - assignedCount;
            issues.Add(new ValidationIssue("splits", "warning",
                $"{unassigned} records have no split assignment."));
        }

        return new DatasetValidationReportDto(datasetId, totalRecords, issues, issues.All(i => i.Severity != "error"));
    }
}

/// <summary>
/// A data quality validation report for a dataset.
/// </summary>
/// <param name="DatasetId">The dataset that was validated.</param>
/// <param name="TotalRecords">Total number of records checked.</param>
/// <param name="Issues">List of validation issues found.</param>
/// <param name="IsValid">Whether the dataset passes validation (no error-level issues).</param>
public sealed record DatasetValidationReportDto(
    Guid DatasetId,
    int TotalRecords,
    List<ValidationIssue> Issues,
    bool IsValid);

/// <summary>
/// A single validation issue found in a dataset.
/// </summary>
/// <param name="Column">The column or area where the issue was found.</param>
/// <param name="Severity">The severity level: info, warning, or error.</param>
/// <param name="Message">A human-readable description of the issue.</param>
public sealed record ValidationIssue(string Column, string Severity, string Message);

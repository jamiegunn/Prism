using Prism.Features.Datasets.Application.ValidateDataset;

namespace Prism.Tests.Unit.Datasets;

public sealed class ValidateDatasetTests
{
    [Fact]
    public void ValidationReport_NoIssues_IsValid()
    {
        var report = new DatasetValidationReportDto(
            Guid.NewGuid(),
            TotalRecords: 10,
            Issues: [],
            IsValid: true);

        report.IsValid.Should().BeTrue();
        report.Issues.Should().BeEmpty();
    }

    [Fact]
    public void ValidationReport_WithErrorIssue_NotValid()
    {
        List<ValidationIssue> issues =
        [
            new ValidationIssue("col1", "error", "Column 'col1' has 8 null/missing values (80.0%).")
        ];

        var report = new DatasetValidationReportDto(
            Guid.NewGuid(),
            TotalRecords: 10,
            Issues: issues,
            IsValid: false);

        report.IsValid.Should().BeFalse();
        report.Issues.Should().HaveCount(1);
        report.Issues[0].Severity.Should().Be("error");
    }

    [Fact]
    public void ValidationReport_WithWarningOnly_StillValid()
    {
        List<ValidationIssue> issues =
        [
            new ValidationIssue("col2", "warning", "Column 'col2' has 3 values that don't match expected type 'number'.")
        ];

        var report = new DatasetValidationReportDto(
            Guid.NewGuid(),
            TotalRecords: 20,
            Issues: issues,
            IsValid: true);

        report.IsValid.Should().BeTrue();
        report.Issues.Should().ContainSingle();
    }

    [Fact]
    public void ValidationIssue_ConstructsCorrectly()
    {
        var issue = new ValidationIssue("my_column", "warning", "Something looks off.");

        issue.Column.Should().Be("my_column");
        issue.Severity.Should().Be("warning");
        issue.Message.Should().Be("Something looks off.");
    }
}

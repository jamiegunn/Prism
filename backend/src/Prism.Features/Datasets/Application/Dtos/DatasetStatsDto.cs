namespace Prism.Features.Datasets.Application.Dtos;

/// <summary>
/// Statistics for a dataset including per-column distributions.
/// </summary>
/// <param name="RecordCount">The total number of records.</param>
/// <param name="SplitDistribution">Record counts per split label.</param>
/// <param name="ColumnStats">Per-column statistics.</param>
public sealed record DatasetStatsDto(
    int RecordCount,
    Dictionary<string, int> SplitDistribution,
    List<ColumnStatsDto> ColumnStats);

/// <summary>
/// Statistics for a single column in a dataset.
/// </summary>
/// <param name="ColumnName">The column name.</param>
/// <param name="NonNullCount">The number of non-null values.</param>
/// <param name="NullCount">The number of null values.</param>
/// <param name="UniqueCount">The number of distinct values.</param>
/// <param name="TopValues">The most frequent values and their counts.</param>
public sealed record ColumnStatsDto(
    string ColumnName,
    int NonNullCount,
    int NullCount,
    int UniqueCount,
    Dictionary<string, int> TopValues);

using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.Dtos;

/// <summary>
/// Data transfer object for a dataset.
/// </summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="ProjectId">The optional parent project identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The optional description.</param>
/// <param name="Format">The source file format.</param>
/// <param name="Schema">The column schema definitions.</param>
/// <param name="RecordCount">The total number of records.</param>
/// <param name="SizeBytes">The source file size in bytes.</param>
/// <param name="Version">The current version number.</param>
/// <param name="Splits">The named splits in this dataset.</param>
/// <param name="CreatedAt">When the dataset was created.</param>
/// <param name="UpdatedAt">When the dataset was last updated.</param>
public sealed record DatasetDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string? Description,
    string Format,
    List<ColumnSchema> Schema,
    int RecordCount,
    long SizeBytes,
    int Version,
    List<DatasetSplitDto> Splits,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    /// <param name="entity">The dataset entity.</param>
    /// <returns>A new DTO instance.</returns>
    public static DatasetDto FromEntity(Dataset entity) => new(
        entity.Id,
        entity.ProjectId,
        entity.Name,
        entity.Description,
        entity.Format.ToString(),
        entity.Schema,
        entity.RecordCount,
        entity.SizeBytes,
        entity.Version,
        entity.Splits.Select(DatasetSplitDto.FromEntity).ToList(),
        entity.CreatedAt,
        entity.UpdatedAt);
}

/// <summary>
/// Data transfer object for a dataset split.
/// </summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="Name">The split name (e.g., "train", "test", "val").</param>
/// <param name="RecordCount">The number of records in this split.</param>
public sealed record DatasetSplitDto(Guid Id, string Name, int RecordCount)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    /// <param name="entity">The split entity.</param>
    /// <returns>A new DTO instance.</returns>
    public static DatasetSplitDto FromEntity(DatasetSplit entity) => new(
        entity.Id, entity.Name, entity.RecordCount);
}

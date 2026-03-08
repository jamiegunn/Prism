using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.Dtos;

/// <summary>
/// Data transfer object for a dataset record.
/// </summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="DatasetId">The parent dataset identifier.</param>
/// <param name="Data">The record data as key-value pairs.</param>
/// <param name="SplitLabel">The split label (train/test/val) or null.</param>
/// <param name="OrderIndex">The position in the dataset.</param>
public sealed record DatasetRecordDto(
    Guid Id,
    Guid DatasetId,
    Dictionary<string, object?> Data,
    string? SplitLabel,
    int OrderIndex)
{
    /// <summary>
    /// Creates a DTO from a domain entity.
    /// </summary>
    /// <param name="entity">The record entity.</param>
    /// <returns>A new DTO instance.</returns>
    public static DatasetRecordDto FromEntity(DatasetRecord entity) => new(
        entity.Id, entity.DatasetId, entity.Data, entity.SplitLabel, entity.OrderIndex);
}

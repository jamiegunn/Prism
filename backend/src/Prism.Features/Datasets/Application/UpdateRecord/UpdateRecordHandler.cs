using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.UpdateRecord;

/// <summary>
/// Command to update a single dataset record's data.
/// </summary>
/// <param name="DatasetId">The parent dataset identifier.</param>
/// <param name="RecordId">The record identifier.</param>
/// <param name="Data">The updated record data.</param>
public sealed record UpdateRecordCommand(Guid DatasetId, Guid RecordId, Dictionary<string, object?> Data);

/// <summary>
/// Handles inline editing of a dataset record.
/// </summary>
public sealed class UpdateRecordHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateRecordHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public UpdateRecordHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Updates a record's data.
    /// </summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated record DTO.</returns>
    public async Task<Result<DatasetRecordDto>> HandleAsync(UpdateRecordCommand command, CancellationToken ct)
    {
        DatasetRecord? record = await _db.Set<DatasetRecord>()
            .FirstOrDefaultAsync(r => r.Id == command.RecordId && r.DatasetId == command.DatasetId, ct);

        if (record is null)
        {
            return Error.NotFound($"Record {command.RecordId} not found in dataset {command.DatasetId}.");
        }

        record.Data = command.Data;
        record.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return DatasetRecordDto.FromEntity(record);
    }
}

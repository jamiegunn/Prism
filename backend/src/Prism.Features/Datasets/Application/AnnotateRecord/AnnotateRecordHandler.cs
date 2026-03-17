using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.AnnotateRecord;

/// <summary>
/// Handles annotating a dataset record with a label, notes, and correctness flag.
/// </summary>
public sealed class AnnotateRecordHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnnotateRecordHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public AnnotateRecordHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Annotates a dataset record with a label, notes, and/or correctness flag.
    /// </summary>
    /// <param name="command">The annotation command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(AnnotateRecordCommand command, CancellationToken ct)
    {
        DatasetRecord? record = await _db.Set<DatasetRecord>()
            .FirstOrDefaultAsync(r => r.Id == command.RecordId && r.DatasetId == command.DatasetId, ct);

        if (record is null)
        {
            return Result.Failure(Error.NotFound($"Record {command.RecordId} not found in dataset {command.DatasetId}."));
        }

        record.AnnotationLabel = command.Label;
        record.AnnotationNote = command.Note;
        record.IsCorrect = command.IsCorrect;
        record.AnnotatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Command for annotating a dataset record.
/// </summary>
/// <param name="DatasetId">The parent dataset ID.</param>
/// <param name="RecordId">The record to annotate.</param>
/// <param name="Label">Optional annotation label.</param>
/// <param name="Note">Optional annotation note.</param>
/// <param name="IsCorrect">Optional correctness flag.</param>
public sealed record AnnotateRecordCommand(
    Guid DatasetId,
    Guid RecordId,
    string? Label = null,
    string? Note = null,
    bool? IsCorrect = null);

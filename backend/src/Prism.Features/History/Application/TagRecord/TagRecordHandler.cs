using Microsoft.EntityFrameworkCore;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Application.TagRecord;

/// <summary>
/// Handles updating the tags on an existing inference record.
/// </summary>
public sealed class TagRecordHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<TagRecordHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagRecordHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public TagRecordHandler(AppDbContext db, ILogger<TagRecordHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Replaces the tags on the specified inference record with the provided tag list.
    /// </summary>
    /// <param name="command">The command containing the record ID and new tags.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success, or a not-found error if the record does not exist.</returns>
    public async Task<Result> HandleAsync(TagRecordCommand command, CancellationToken ct)
    {
        InferenceRecord? record = await _db.Set<InferenceRecord>()
            .FirstOrDefaultAsync(r => r.Id == command.Id, ct);

        if (record is null)
        {
            _logger.LogWarning("Inference record {RecordId} was not found for tagging", command.Id);
            return Result.Failure(Error.NotFound($"Inference record '{command.Id}' was not found."));
        }

        record.Tags = command.Tags;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated tags on inference record {RecordId} to {Tags}", command.Id, command.Tags);
        return Result.Success();
    }
}

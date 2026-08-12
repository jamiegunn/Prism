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

        // Normalize server-side rather than trusting the client. A null list (JSON
        // `{"tags":null}`) reached a required non-nullable column and threw a 500; null,
        // blank, and duplicate entries slipped past the frontend's own trim/lowercase/dedupe
        // whenever the caller was not the UI. The API is the source of truth for what a valid
        // tag set is, so it enforces the same contract the tag filter depends on.
        record.Tags = NormalizeTags(command.Tags);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated tags on inference record {RecordId} to {Tags}", command.Id, record.Tags);
        return Result.Success();
    }

    /// <summary>
    /// Reduces an arbitrary caller-supplied tag list to the canonical form the tag filter
    /// matches against: trimmed, lowercased, non-empty, de-duplicated, order preserved. A
    /// null list becomes an empty list — clearing the tags, not crashing on a required column.
    /// </summary>
    /// <param name="tags">The tags as supplied, possibly null or containing null/blank entries.</param>
    /// <returns>The normalized tag list.</returns>
    internal static List<string> NormalizeTags(IEnumerable<string?>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (string? tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            string normalized = tag.Trim().ToLowerInvariant();
            if (seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }
}

using Microsoft.EntityFrameworkCore;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.QuerySets;

/// <summary>
/// Command to create a labelled query set for a collection.
/// </summary>
/// <param name="CollectionId">The collection the set labels.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Items">The labelled queries: text plus relevant chunk ids.</param>
public sealed record CreateQuerySetCommand(
    Guid CollectionId,
    string Name,
    string? Description,
    List<(string QueryText, List<Guid> RelevantChunkIds)> Items);

/// <summary>
/// Creates a labelled query set, validating that every labelled chunk actually belongs to
/// the collection — a label pointing at another collection's chunk would silently score
/// every retrieval as a miss.
/// </summary>
public sealed class CreateQuerySetHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateQuerySetHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public CreateQuerySetHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the create command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The created set, or a validation error.</returns>
    public async Task<Result<RagQuerySetDto>> HandleAsync(CreateQuerySetCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Error.Validation("Query set name is required.");
        }

        // Null is treated as empty rather than trusted: an omitted `items` used to reach LINQ and
        // return a 500 saying "Value cannot be null. (Parameter 'source')", which names an
        // internal argument instead of the field the caller left out.
        if (command.Items is not { Count: > 0 })
        {
            return Error.Validation("A query set needs at least one labelled query.");
        }

        foreach ((string queryText, List<Guid> relevantIds) in command.Items)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return Error.Validation("Every item needs query text.");
            }

            if (relevantIds is not { Count: > 0 })
            {
                return Error.Validation(
                    $"Query '{Truncate(queryText)}' has no relevant chunks labelled. " +
                    "An item with no relevant chunks cannot be scored.");
            }
        }

        bool collectionExists = await _db.Set<RagCollection>()
            .AnyAsync(c => c.Id == command.CollectionId, ct);

        if (!collectionExists)
        {
            return Error.NotFound($"RAG collection {command.CollectionId} not found.");
        }

        // Every labelled chunk must belong to this collection.
        List<Guid> labelledIds = command.Items
            .SelectMany(i => i.RelevantChunkIds)
            .Distinct()
            .ToList();

        List<Guid> knownIds = await _db.Set<RagChunk>()
            .Where(c => labelledIds.Contains(c.Id))
            .Join(
                _db.Set<RagDocument>().Where(d => d.CollectionId == command.CollectionId),
                chunk => chunk.DocumentId,
                doc => doc.Id,
                (chunk, _) => chunk.Id)
            .ToListAsync(ct);

        List<Guid> unknown = labelledIds.Except(knownIds).ToList();

        if (unknown.Count > 0)
        {
            return Error.Validation(
                $"{unknown.Count} labelled chunk id(s) do not belong to this collection " +
                $"(first: {unknown[0]}).");
        }

        var querySet = new RagQuerySet
        {
            CollectionId = command.CollectionId,
            Name = command.Name.Trim(),
            Description = command.Description,
            Items = command.Items
                .Select((item, index) => new RagQuerySetItem
                {
                    QueryText = item.QueryText.Trim(),
                    RelevantChunkIds = item.RelevantChunkIds.Distinct().ToList(),
                    OrderIndex = index,
                })
                .ToList(),
        };

        _db.Add(querySet);
        await _db.SaveChangesAsync(ct);

        return new RagQuerySetDto(
            querySet.Id,
            querySet.CollectionId,
            querySet.Name,
            querySet.Description,
            querySet.Items.Count,
            querySet.CreatedAt);
    }

    private static string Truncate(string text) =>
        text.Length <= 40 ? text : text[..40] + "…";
}

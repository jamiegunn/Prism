using Microsoft.EntityFrameworkCore;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.QuerySets;

/// <summary>
/// Deletes a labelled query set and its items.
/// </summary>
public sealed class DeleteQuerySetHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteQuerySetHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public DeleteQuerySetHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the delete.
    /// </summary>
    /// <param name="querySetId">The set to delete.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>Success, or NotFound.</returns>
    public async Task<Result> HandleAsync(Guid querySetId, CancellationToken ct)
    {
        RagQuerySet? querySet = await _db.Set<RagQuerySet>()
            .FirstOrDefaultAsync(s => s.Id == querySetId, ct);

        if (querySet is null)
        {
            return Error.NotFound($"Query set {querySetId} not found.");
        }

        _db.Remove(querySet);
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }
}

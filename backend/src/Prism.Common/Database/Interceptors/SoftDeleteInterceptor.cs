using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Prism.Common.Database.Interceptors;

/// <summary>
/// Marks an entity as supporting soft deletion instead of physical deletion.
/// Entities implementing this interface will have their delete operations converted
/// to update operations by the <see cref="SoftDeleteInterceptor"/>.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Gets or sets a value indicating whether the entity has been soft-deleted.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was soft-deleted.
    /// Null if the entity has not been deleted.
    /// </summary>
    DateTime? DeletedAt { get; set; }
}

/// <summary>
/// EF Core interceptor that converts physical delete operations on <see cref="ISoftDeletable"/>
/// entities into update operations that set the <see cref="ISoftDeletable.IsDeleted"/> flag.
/// </summary>
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Intercepts the synchronous SaveChanges call to convert deletes to soft deletes.
    /// </summary>
    /// <param name="eventData">The event data containing the DbContext.</param>
    /// <param name="result">The interception result.</param>
    /// <returns>The interception result after converting delete operations.</returns>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ConvertToSoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Intercepts the asynchronous SaveChangesAsync call to convert deletes to soft deletes.
    /// </summary>
    /// <param name="eventData">The event data containing the DbContext.</param>
    /// <param name="result">The interception result.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The interception result after converting delete operations.</returns>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ConvertToSoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ConvertToSoftDelete(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime utcNow = DateTime.UtcNow;

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ISoftDeletable> entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = utcNow;
        }
    }
}

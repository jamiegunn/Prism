using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Prism.Common.Database.Interceptors;

/// <summary>
/// EF Core interceptor that automatically sets <see cref="BaseEntity.CreatedAt"/> and
/// <see cref="BaseEntity.UpdatedAt"/> timestamps on entities inheriting from <see cref="BaseEntity"/>.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Intercepts the synchronous SaveChanges call to set audit timestamps.
    /// </summary>
    /// <param name="eventData">The event data containing the DbContext.</param>
    /// <param name="result">The interception result.</param>
    /// <returns>The interception result after setting timestamps.</returns>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SetAuditTimestamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Intercepts the asynchronous SaveChangesAsync call to set audit timestamps.
    /// </summary>
    /// <param name="eventData">The event data containing the DbContext.</param>
    /// <param name="result">The interception result.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The interception result after setting timestamps.</returns>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetAuditTimestamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void SetAuditTimestamps(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime utcNow = DateTime.UtcNow;

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseEntity> entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    break;
            }
        }
    }
}

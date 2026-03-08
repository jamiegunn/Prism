namespace Prism.Common.Database.Seeders;

/// <summary>
/// Defines a contract for seeding initial data into the database.
/// Implementations are discovered via DI and executed in order by <see cref="SeedDataRunner"/>.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Gets the execution order for this seeder. Lower values run first.
    /// Use increments of 10 to allow insertion of new seeders between existing ones.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Seeds data into the database using the provided context.
    /// Implementations should check for existing data to ensure idempotency.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    Task SeedAsync(AppDbContext context, CancellationToken ct);
}

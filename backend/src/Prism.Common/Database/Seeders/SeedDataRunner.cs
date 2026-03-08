using Microsoft.Extensions.DependencyInjection;

namespace Prism.Common.Database.Seeders;

/// <summary>
/// Discovers and executes all registered <see cref="IDataSeeder"/> implementations in order.
/// Runs seeders only if the database has not been previously seeded, determined by the
/// presence of a seed marker in the database.
/// </summary>
public sealed class SeedDataRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeedDataRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeedDataRunner"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving seeders.</param>
    /// <param name="logger">The logger for recording seed operations.</param>
    public SeedDataRunner(IServiceProvider serviceProvider, ILogger<SeedDataRunner> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Executes all registered seeders in order. Each seeder is responsible for
    /// checking whether its data already exists to ensure idempotency.
    /// </summary>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seed operation.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        IEnumerable<IDataSeeder> seeders = scope.ServiceProvider
            .GetServices<IDataSeeder>()
            .OrderBy(s => s.Order);

        List<IDataSeeder> seederList = seeders.ToList();

        if (seederList.Count == 0)
        {
            _logger.LogInformation("No data seeders registered, skipping seed phase");
            return;
        }

        _logger.LogInformation("Running {SeederCount} data seeders", seederList.Count);

        foreach (IDataSeeder seeder in seederList)
        {
            string seederName = seeder.GetType().Name;
            _logger.LogInformation("Executing seeder {SeederName} (order: {Order})", seederName, seeder.Order);

            try
            {
                await seeder.SeedAsync(context, ct);
                _logger.LogInformation("Seeder {SeederName} completed successfully", seederName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Seeder {SeederName} failed with error", seederName);
                throw;
            }
        }

        _logger.LogInformation("All data seeders completed successfully");
    }
}

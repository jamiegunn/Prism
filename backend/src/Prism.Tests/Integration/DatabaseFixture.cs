using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Prism.Common.Database;
using Testcontainers.PostgreSql;

namespace Prism.Tests.Integration;

/// <summary>
/// Shared fixture providing a real PostgreSQL database and a correctly configured
/// <see cref="AppDbContext"/> for integration tests.
/// </summary>
/// <remarks>
/// <para>
/// Uses an externally supplied database when <c>PRISM_TEST_DB</c> is set, so the suite runs
/// where no Docker daemon is available; otherwise starts a Testcontainers pgvector container.
/// </para>
/// <para>
/// The context is configured exactly as the API host configures it — feature assemblies
/// registered, pgvector type mapping enabled. Configuring a fixture differently from the
/// application is how a suite ends up asserting against a model the application never uses.
/// </para>
/// </remarks>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string ExternalConnectionStringVariable = "PRISM_TEST_DB";

    private PostgreSqlContainer? _container;
    private string? _connectionString;

    /// <summary>
    /// Gets the assemblies forming the EF model, matching the API host's registration.
    /// </summary>
    public static ModelAssemblies ModelAssemblies { get; } =
        new(typeof(Prism.Features.Marker).Assembly);

    /// <summary>
    /// Creates a context against the fixture's database.
    /// </summary>
    /// <returns>A configured <see cref="AppDbContext"/>.</returns>
    public AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options, ModelAssemblies);
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        string? external = Environment.GetEnvironmentVariable(ExternalConnectionStringVariable);

        if (!string.IsNullOrWhiteSpace(external))
        {
            _connectionString = external;
        }
        else
        {
            _container = new PostgreSqlBuilder()
                .WithImage("pgvector/pgvector:pg16")
                .WithDatabase("prism_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        await using AppDbContext context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

/// <summary>
/// xUnit collection binding for tests sharing <see cref="DatabaseFixture"/>.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}

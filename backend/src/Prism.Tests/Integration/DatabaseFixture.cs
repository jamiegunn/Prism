using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Testcontainers.PostgreSql;

namespace Prism.Tests.Integration;

/// <summary>
/// Shared fixture that provides a PostgreSQL test container and configured DbContext.
/// Used by integration tests that need a real database.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("prism_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _container.StartAsync();

        // Apply migrations
        await using AppDbContext context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}

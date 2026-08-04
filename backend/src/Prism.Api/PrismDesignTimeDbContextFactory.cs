using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;
using Prism.Common.Database;

namespace Prism.Api;

/// <summary>
/// Supplies <see cref="AppDbContext"/> to design-time tooling (<c>dotnet ef migrations add</c>,
/// <c>dotnet ef database update</c>) with the full set of model assemblies.
/// </summary>
/// <remarks>
/// Without this factory the tooling builds the context through the application host. If that
/// path ever fails to contribute the feature assemblies, Entity Framework compares a complete
/// migrations snapshot against a near-empty model and emits a migration that drops every table.
/// Declaring the factory removes the tooling's dependence on host startup succeeding.
/// </remarks>
public sealed class PrismDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5438;Database=prism;Username=postgres;Password=postgres";

    /// <summary>
    /// Creates a context for design-time use.
    /// </summary>
    /// <param name="args">Arguments supplied by the tooling. Unused.</param>
    /// <returns>A context configured with every model assembly.</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("DATABASE__CONNECTIONSTRING")
            ?? DefaultConnectionString;

        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options, new ModelAssemblies(typeof(Prism.Features.Marker).Assembly));
    }
}

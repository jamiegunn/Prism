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
/// <para>
/// The external database is emptied on startup so that both paths behave the same way. A
/// Testcontainers run gets a brand-new database every time; without truncation an external
/// one accumulates every row every previous run wrote, and any test that asserts on an
/// aggregate starts passing in CI while failing locally on the second run of the day. That
/// asymmetry is worse than either behaviour on its own, because it makes the failure look
/// like flakiness rather than a real difference in setup.
/// </para>
/// </remarks>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string ExternalConnectionStringVariable = "PRISM_TEST_DB";

    /// <summary>
    /// The database name the application itself uses. Refusing to truncate it is the one
    /// guard against someone exporting their development connection string by mistake.
    /// </summary>
    private const string ApplicationDatabaseName = "prism";

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

        bool isExternal = !string.IsNullOrWhiteSpace(external);

        if (isExternal)
        {
            _connectionString = external;
            GuardAgainstApplicationDatabase(external!);
            await EnsureDatabaseExistsAsync(external!);
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

        if (isExternal)
        {
            await TruncateAllTablesAsync(context);
        }
    }

    /// <summary>
    /// Creates the target database when the server does not already have it.
    /// </summary>
    /// <param name="connectionString">The externally supplied connection string.</param>
    /// <remarks>
    /// <para>
    /// EF's <c>MigrateAsync</c> creates the schema but not the database, so pointing
    /// <c>PRISM_TEST_DB</c> at a server that has never seen this project fails with
    /// <c>3D000: database "prism_test" does not exist</c> — a message that reads like a
    /// misconfiguration rather than a one-line fix.
    /// </para>
    /// <para>
    /// Doing it here rather than in the setup scripts means it works from any entry point:
    /// a bare <c>dotnet test</c>, an editor's test runner, CI, or the pre-commit hook. It also
    /// needs no <c>psql</c> on the path and does not care whether the server came from this
    /// repository's compose file or was already on the machine.
    /// </para>
    /// </remarks>
    private static async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        var target = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        string databaseName = target.Database
            ?? throw new InvalidOperationException(
                $"{ExternalConnectionStringVariable} does not name a database.");

        // Connect to the maintenance database, which every PostgreSQL server has.
        var maintenance = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres",
        };

        await using var connection = new Npgsql.NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();

        await using (Npgsql.NpgsqlCommand exists = connection.CreateCommand())
        {
            exists.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
            exists.Parameters.AddWithValue("name", databaseName);

            if (await exists.ExecuteScalarAsync() is not null)
            {
                return;
            }
        }

        // CREATE DATABASE cannot be parameterised, so the identifier is quoted instead.
        await using Npgsql.NpgsqlCommand create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{databaseName.Replace("\"", "\"\"")}\"";
        await create.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Refuses to run against the application's own database.
    /// </summary>
    /// <param name="connectionString">The externally supplied connection string.</param>
    internal static void GuardAgainstApplicationDatabase(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        if (string.Equals(builder.Database, ApplicationDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ExternalConnectionStringVariable} points at '{ApplicationDatabaseName}', which is the "
                + "database the application uses. The test fixture empties whatever it is given, so it "
                + "will not run against that. Point it at a separate database, for example 'prism_test'.");
        }
    }

    /// <summary>
    /// Empties every application table, leaving the schema and migration history intact.
    /// </summary>
    /// <param name="context">A context against the external database.</param>
    /// <remarks>
    /// One statement so that foreign keys never see a partially emptied graph, and
    /// <c>RESTART IDENTITY</c> so identity columns do not drift upward run after run.
    /// </remarks>
    private static async Task TruncateAllTablesAsync(AppDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            DO $$
            DECLARE
                statement text;
            BEGIN
                SELECT 'TRUNCATE TABLE ' || string_agg(format('%I.%I', schemaname, tablename), ', ')
                     || ' RESTART IDENTITY CASCADE'
                INTO statement
                FROM pg_tables
                WHERE schemaname = 'public'
                  AND tablename <> '__EFMigrationsHistory';

                IF statement IS NOT NULL THEN
                    EXECUTE statement;
                END IF;
            END $$;
            """);
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

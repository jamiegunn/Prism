# ADR-008: Database Abstraction via EF Core

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The platform uses PostgreSQL as the primary data store. However, future scenarios include:

- Switching to a managed database service
- Using SQLite for lightweight dev/testing without Docker
- Migrating to SQL Server or CosmosDB for enterprise deployments

Additionally, the vertical slice architecture needs a clear strategy for:
- Where database context and migrations live
- How features define their own tables without creating per-feature DbContexts
- How migrations are applied in dev vs. production

## Decision

**EF Core IS the database abstraction.** We do not create a custom `IDatabase` interface — EF Core already abstracts the database provider. The key constraints:

1. **No raw SQL or Postgres-specific features in feature code.** All queries use LINQ/EF Core abstractions. Postgres-specific features (e.g., `jsonb`, `citext`, array columns) are configured in `IEntityTypeConfiguration<T>` only — features query through LINQ.

2. **Single `AppDbContext`** in `Common/Database/` — not per-feature. Multiple DbContexts fragment the connection pool and make cross-feature queries impossible.

3. **Features register entity configurations** via `IEntityTypeConfiguration<T>` in their own `Infrastructure/` folder. The AppDbContext discovers them by assembly scanning.

4. **Migrations live in `Common/Database/Migrations/`** — a single linear migration history.

5. **Feature tables use a feature prefix:** `playground_sessions`, `experiments_runs`, `history_records`, `rag_collections`.

### Migration Strategy

| Environment | Strategy | How |
|-------------|----------|-----|
| Development | Auto-migrate on startup | `context.Database.MigrateAsync()` in `Program.cs` (dev only) |
| CI/Testing | Fresh database per test | Testcontainers spins up Postgres, applies all migrations |
| Production | Explicit migration scripts | `dotnet ef migrations script --idempotent` generates SQL, applied by deployment pipeline |

### AppDbContext Registration

```csharp
/// <summary>
/// Central database context for the entire application.
/// Each feature registers its entity configurations via IEntityTypeConfiguration.
/// The context discovers all configurations by scanning the Features assembly.
/// </summary>
public sealed class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Scan Features assembly for all IEntityTypeConfiguration implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Apply common conventions
        modelBuilder.ApplyCommonConventions();  // e.g., snake_case naming, UTC dates
    }
}
```

### Feature Entity Configuration Example

```csharp
// In Features/Experiments/Infrastructure/ExperimentConfiguration.cs
public sealed class ExperimentConfiguration : IEntityTypeConfiguration<Experiment>
{
    public void Configure(EntityTypeBuilder<Experiment> builder)
    {
        builder.ToTable("experiments_experiments");  // prefix_entity
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Parameters).HasColumnType("jsonb");
        builder.HasMany(e => e.Runs).WithOne().HasForeignKey(r => r.ExperimentId);
    }
}
```

### Database Provider Swap

```csharp
// In ServiceCollectionExtensions — switch via config
services.AddCommonDatabase(config);

// Reads "Database:Provider" from appsettings.json
// "Postgres"  -> UseNpgsql(connectionString)
// "SqlServer" -> UseSqlServer(connectionString)
// "Sqlite"    -> UseSqlite(connectionString)  // for lightweight dev/testing
```

```json
{
  "Database": {
    "Provider": "Postgres",
    "ConnectionString": "Host=localhost;Database=ai_research;Username=postgres;Password=...",
    "EnableSensitiveDataLogging": false,
    "MaxRetryCount": 3
  }
}
```

## Consequences

### Positive

- No custom abstraction layer — EF Core is the industry standard ORM and database abstraction
- Single DbContext avoids connection pool fragmentation
- Features define their own tables via `IEntityTypeConfiguration<T>` without modifying shared code
- Database provider swap is a one-line change in DI registration
- Auto-migrate in dev eliminates manual migration steps during development
- Feature-prefixed table names prevent naming collisions

### Negative

- All features share one migration history — a migration touching `experiments_runs` and `playground_sessions` will be in the same migration file if generated together
  - Mitigated: generate migrations per logical change, not per `dotnet ef migrations add`
- LINQ-only constraint means some Postgres-specific optimizations (e.g., `pg_trgm` for fuzzy search) must be wrapped in extension methods or raw SQL in infrastructure layer only
- Auto-migrate in dev can be dangerous if migration is destructive — use `dotnet ef migrations script` to review first

### Neutral

- Interceptors handle cross-cutting concerns: `AuditInterceptor` (CreatedAt/UpdatedAt), `SoftDeleteInterceptor` (IsDeleted flag)
- JSON columns (`jsonb` in Postgres, `nvarchar(max)` in SQL Server) are configured per-entity, not globally
- Connection string is the only Postgres-specific value — everything else is EF Core abstractions

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Custom `IRepository<T>` over EF Core | Full abstraction | Duplicates what EF Core already provides, loses IQueryable | EF Core's DbSet IS the repository |
| Dapper (micro-ORM) | Maximum SQL control, performance | No migrations, no change tracking, manual mapping | Productivity loss not justified for a research tool |
| Per-feature DbContext | Maximum isolation | Connection pool fragmentation, no cross-feature queries, migration headaches | Single context with feature configurations achieves isolation without fragmentation |
| Supabase | Postgres + auth + storage + realtime | Duplicates abstractions we already built (IAuthProvider, IFileStorage), adds hosted dependency | We own our own abstractions; Supabase's value is the hosted layer we don't need |

## References

- See `ARCHITECTURE.md` — Backend Project Structure, `Common/Database/` section
- [EF Core Database Providers](https://learn.microsoft.com/en-us/ef/core/providers/)

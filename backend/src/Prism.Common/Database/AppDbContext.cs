using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Prism.Common.Database;

/// <summary>
/// The single application database context for the Prism platform.
/// Applies entity configurations from the Common assembly and from every assembly
/// supplied via <see cref="ModelAssemblies"/>.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private readonly ModelAssemblies _modelAssemblies;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    /// <param name="modelAssemblies">
    /// The assemblies whose entity type configurations form the model. Required: omitting the
    /// feature assemblies produces a model with one entity instead of thirty-one, which Entity
    /// Framework then diffs against the migrations snapshot as "drop every table".
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="modelAssemblies"/> is <see langword="null"/>.</exception>
    public AppDbContext(DbContextOptions<AppDbContext> options, ModelAssemblies modelAssemblies)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(modelAssemblies);
        _modelAssemblies = modelAssemblies;
    }

    /// <summary>
    /// Configures the entity model by scanning the Common assembly and every assembly
    /// supplied through <see cref="ModelAssemblies"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder used to construct the entity model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (Assembly assembly in _modelAssemblies.Assemblies)
        {
            if (assembly != typeof(AppDbContext).Assembly)
            {
                modelBuilder.ApplyConfigurationsFromAssembly(assembly);
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}

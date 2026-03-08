using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Prism.Common.Database;

/// <summary>
/// The single application database context for the Prism platform.
/// Automatically discovers and applies entity configurations from the Common assembly
/// and any additional assemblies registered via <see cref="RegisterAssembly"/>.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private static readonly List<Assembly> _additionalAssemblies = new();

    /// <summary>
    /// Registers an additional assembly whose <see cref="IEntityTypeConfiguration{TEntity}"/> implementations
    /// should be discovered and applied during model building.
    /// Call this from feature module registration (e.g., in Program.cs) before the first DbContext is created.
    /// </summary>
    /// <param name="assembly">The assembly to scan for entity configurations.</param>
    public static void RegisterAssembly(Assembly assembly)
    {
        if (!_additionalAssemblies.Contains(assembly))
        {
            _additionalAssemblies.Add(assembly);
        }
    }

    /// <summary>
    /// Clears all registered additional assemblies.
    /// Primarily used for testing scenarios.
    /// </summary>
    public static void ClearRegisteredAssemblies()
    {
        _additionalAssemblies.Clear();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Configures the entity model by scanning for <see cref="IEntityTypeConfiguration{TEntity}"/>
    /// implementations in the Common assembly and all registered additional assemblies.
    /// </summary>
    /// <param name="modelBuilder">The model builder used to construct the entity model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (Assembly assembly in _additionalAssemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        base.OnModelCreating(modelBuilder);
    }
}

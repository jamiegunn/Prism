using System.Reflection;

namespace Prism.Common.Database;

/// <summary>
/// The explicit set of assemblies scanned for <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/>
/// implementations when <see cref="AppDbContext"/> builds its model.
/// </summary>
/// <remarks>
/// <para>
/// This replaces a previous static mutable registry. That design made the database model depend on
/// whether a static method had been called first, by whom, and in what order: a context constructed
/// outside the API host silently produced a model containing a single entity instead of thirty-one.
/// Because Entity Framework compares the model against the migrations snapshot, that empty model
/// yielded a diff proposing to drop every table in the database.
/// </para>
/// <para>
/// Passing the assemblies explicitly makes the failure impossible to reach: there is no way to
/// construct <see cref="AppDbContext"/> without stating which assemblies define its entities.
/// </para>
/// </remarks>
public sealed class ModelAssemblies
{
    private readonly Assembly[] _assemblies;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelAssemblies"/> class.
    /// </summary>
    /// <param name="assemblies">
    /// Assemblies containing entity type configurations. The assembly declaring
    /// <see cref="AppDbContext"/> is always scanned and need not be listed.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="assemblies"/> contains a <see langword="null"/> entry.</exception>
    public ModelAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        if (Array.Exists(assemblies, a => a is null))
        {
            throw new ArgumentException("Assembly list must not contain null entries.", nameof(assemblies));
        }

        _assemblies = assemblies.Distinct().ToArray();
    }

    /// <summary>
    /// Gets the assemblies to scan, in registration order.
    /// </summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;
}

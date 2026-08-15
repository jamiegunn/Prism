using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Prism.Common.Database;

/// <summary>
/// Creates the database schema directly from the entity model, and refuses to run against a
/// database whose schema no longer matches it.
/// </summary>
/// <remarks>
/// <para>
/// This project does not use EF migrations. The entity configurations are the schema: to
/// change it, change the <c>IEntityTypeConfiguration&lt;T&gt;</c> and recreate the database.
/// Nothing is upgraded in place, and no database anywhere holds data that has to survive a
/// schema change.
/// </para>
/// <para>
/// The hazard that buys is <c>EnsureCreatedAsync</c>'s
/// contract: it creates the schema only when the database has no tables at all, and otherwise
/// does nothing and reports success. A developer who changes an entity and reruns would get a
/// silently stale schema and a stream of errors that point anywhere but here. So the model's
/// own creation script is hashed and recorded on the database, and a mismatch is fatal with
/// an instruction rather than a puzzle.
/// </para>
/// </remarks>
public static class SchemaBootstrapper
{
    /// <summary>
    /// Prefix identifying a schema comment written by this class.
    /// </summary>
    /// <remarks>
    /// PostgreSQL ships its own comment on the <c>public</c> schema — the literal string
    /// "standard public schema" — so an unmarked database does not read back as null. Without
    /// a prefix, a database created before this class existed reports "created from a
    /// different model (found standard public schema)", which sends the reader looking for a
    /// model change that never happened. Anything not carrying this prefix is untracked.
    /// </remarks>
    private const string HashPrefix = "prism-schema:";

    /// <summary>
    /// Ensures the database exists and its schema matches the current entity model.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <param name="resetIfStale">
    /// When <see langword="true"/>, a schema that does not match the model is dropped and
    /// rebuilt instead of throwing. Only callers that own a disposable database should pass
    /// <see langword="true"/> — the test fixture does; the application never does, because
    /// silently destroying a developer's database is worse than refusing to start.
    /// </param>
    /// <returns><see langword="true"/> if the schema was created or rebuilt by this call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="db"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The database already exists but was built from a different model, and
    /// <paramref name="resetIfStale"/> is <see langword="false"/>. It must be dropped and
    /// recreated — see <c>./dev.sh</c>, which offers to do exactly that.
    /// </exception>
    public static async Task<bool> EnsureSchemaAsync(AppDbContext db, CancellationToken ct, bool resetIfStale = false)
    {
        ArgumentNullException.ThrowIfNull(db);

        string expected = ComputeModelHash(db);
        bool created = await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);

        if (created)
        {
            await WriteSchemaHashAsync(db, expected, ct).ConfigureAwait(false);
            Log.Information("Created database schema from the entity model {SchemaHash}", expected);
            return true;
        }

        string? actual = await ReadSchemaHashAsync(db, ct).ConfigureAwait(false);

        if (actual == expected)
        {
            Log.Information("Database schema matches the entity model {SchemaHash}", expected);
            return false;
        }

        // Distinguish the two ways to get here, because the fix is the same but the surprise
        // is not: a database left over from the migrations era has no hash at all.
        // A database the fixture owns has nothing worth preserving, so rebuild rather than
        // make a human intervene. This is the difference between a guard and an obstacle.
        if (resetIfStale)
        {
            Log.Information("Schema is stale; rebuilding it from the model {SchemaHash}", expected);
            await db.Database.ExecuteSqlRawAsync(
                "DROP SCHEMA public CASCADE; CREATE SCHEMA public;", ct).ConfigureAwait(false);
            await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);
            await WriteSchemaHashAsync(db, expected, ct).ConfigureAwait(false);
            return true;
        }

        string cause = actual is null
            ? "it predates schema-hash tracking (most likely it was created by the old EF migrations)"
            : $"it was created from a different model (found {actual}, expected {expected})";

        throw new InvalidOperationException(
            $"The database schema is stale: {cause}. This project creates the schema from the " +
            "entity model and never migrates in place, so the database must be dropped and " +
            "recreated. Run ./dev.sh and answer yes to \"initialise the database\", or drop it " +
            "by hand and restart. All data is reproducible from the seeders.");
    }

    /// <summary>
    /// Hashes the DDL the current model would produce.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <returns>A short hexadecimal digest of the model's creation script.</returns>
    /// <remarks>
    /// The creation script is used rather than the model object graph because it is exactly
    /// what would be applied: two models that generate identical DDL are interchangeable as
    /// far as any running database is concerned, and hashing anything finer would report
    /// drift for changes the database cannot observe.
    /// </remarks>
    private static string ComputeModelHash(AppDbContext db)
    {
        string script = db.Database.GenerateCreateScript();
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(script));
        return Convert.ToHexString(digest)[..16].ToLower(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Records the schema hash as a comment on the <c>public</c> schema.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="hash">The hash to record.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <remarks>
    /// A schema comment is used rather than a table so that the marker needs no entity, no
    /// configuration, and no place in the model it is describing.
    /// </remarks>
    private static async Task WriteSchemaHashAsync(AppDbContext db, string hash, CancellationToken ct)
    {
        // COMMENT ON accepts no parameter placeholder, so the value has to be part of the
        // statement text. Rather than trust that, the hash is re-validated as hexadecimal
        // here: anything else cannot reach the database at all. This also keeps EF1002
        // honest - the SQL is concatenated from a checked value, not interpolated from an
        // arbitrary one.
        if (!IsHex(hash))
        {
            throw new ArgumentException("The schema hash must be hexadecimal.", nameof(hash));
        }

        string sql = string.Concat("COMMENT ON SCHEMA public IS '", HashPrefix, hash, "'");
        await db.Database.ExecuteSqlRawAsync(sql, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether a string consists solely of hexadecimal characters.
    /// </summary>
    /// <param name="value">The string to test.</param>
    /// <returns><see langword="true"/> if every character is a hexadecimal digit.</returns>
    private static bool IsHex(string value)
    {
        foreach (char c in value)
        {
            bool isHexDigit = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHexDigit)
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    /// <summary>
    /// Reads the schema hash previously recorded on the <c>public</c> schema.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The recorded hash, or <see langword="null"/> if none was recorded.</returns>
    private static async Task<string?> ReadSchemaHashAsync(AppDbContext db, CancellationToken ct)
    {
        List<string?> rows = await db.Database
            .SqlQueryRaw<string?>(
                "SELECT obj_description(n.oid, 'pg_namespace') AS \"Value\" " +
                "FROM pg_namespace n WHERE n.nspname = 'public'")
            .ToListAsync(ct)
            .ConfigureAwait(false);

        string? comment = rows.Count > 0 ? rows[0] : null;

        // Anything without the prefix was not written by this class - PostgreSQL's own
        // default comment, or a human's note. Either way the schema is untracked.
        return comment is not null && comment.StartsWith(HashPrefix, StringComparison.Ordinal)
            ? comment[HashPrefix.Length..]
            : null;
    }
}

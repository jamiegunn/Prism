using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Prism.Common.Database;

namespace Prism.Tests.Integration;

/// <summary>
/// Guards against the schema failure modes this project can still have: a model built from an
/// incomplete assembly set, and a database whose schema no longer matches the model.
/// There are no migrations - the entity configurations are the schema.
/// </summary>
[Collection("Database")]
public sealed class SchemaGuardTests
{
    // The model had 31 entity types when this guard was written. The assertion is a floor rather
    // than equality so that adding an entity does not fail the build; the failure it exists to
    // catch is the model collapsing to a handful because assembly registration was missed.
    private const int MinimumExpectedEntityTypes = 31;

    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaGuardTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public SchemaGuardTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// The model must contain every feature's entities, not just the Common assembly's.
    /// </summary>
    [Fact]
    public void Model_Contains_All_Feature_Entities()
    {
        using AppDbContext db = _fixture.CreateContext();

        IReadOnlyList<IEntityType> entityTypes = db.Model.GetEntityTypes().ToList();

        Assert.True(
            entityTypes.Count >= MinimumExpectedEntityTypes,
            $"Model has {entityTypes.Count} entity types, expected at least {MinimumExpectedEntityTypes}. " +
            "A collapsed model means the feature assemblies were not supplied to AppDbContext, " +
            "which makes EF propose dropping every table.");
    }

    /// <summary>
    /// Entities from each feature slice must be present by name, so a partially-registered
    /// model cannot pass the count check by coincidence.
    /// </summary>
    /// <param name="entityName">The CLR type name expected in the model.</param>
    [Theory]
    [InlineData("RagChunk")]
    [InlineData("UsageLog")]
    [InlineData("BatchJob")]
    [InlineData("EvaluationResult")]
    [InlineData("InferenceRecord")]
    [InlineData("PromptTemplate")]
    [InlineData("Dataset")]
    [InlineData("AgentWorkflow")]
    public void Model_Contains_Entity(string entityName)
    {
        using AppDbContext db = _fixture.CreateContext();

        Assert.Contains(db.Model.GetEntityTypes(), e => e.ClrType.Name == entityName);
    }

    /// <summary>
    /// Re-running the bootstrapper against a database it already created must be a no-op.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// This is the false-positive guard. The schema hash is derived from the model's creation
    /// script, and if that script were not stable across calls, every start would declare the
    /// database stale and demand it be dropped. A check that fires when nothing changed gets
    /// switched off, so it is worth a test of its own.
    /// </remarks>
    [Fact]
    public async Task Bootstrapping_An_Existing_Database_Is_A_No_Op()
    {
        await using AppDbContext db = _fixture.CreateContext();

        bool created = await SchemaBootstrapper.EnsureSchemaAsync(db, CancellationToken.None);

        Assert.False(created, "The fixture already created the schema, so nothing should have been created here.");
    }

    /// <summary>
    /// A database whose recorded schema hash does not match the model must be rejected.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// Without migrations, <c>EnsureCreated</c> silently does nothing when tables already
    /// exist. That is the failure this guard exists to convert into an instruction, so the
    /// guard is exercised here by corrupting the marker and asserting the refusal.
    /// </remarks>
    [Fact]
    public async Task Stale_Schema_Is_Rejected()
    {
        await using AppDbContext db = _fixture.CreateContext();

        string? original = await ReadSchemaCommentAsync(db);

        try
        {
            await SetSchemaCommentAsync(db, "deadbeefdeadbeef");

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SchemaBootstrapper.EnsureSchemaAsync(db, CancellationToken.None));

            Assert.Contains("stale", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dev.sh", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            await SetSchemaCommentAsync(db, original ?? string.Empty);
        }
    }

    /// <summary>
    /// A database created before hash tracking existed must be rejected rather than assumed good.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// The comment used here is PostgreSQL's own default, not an empty one. That distinction
    /// matters: a real migrations-era database always carries "standard public schema", so a
    /// test that cleared the comment entirely would exercise a state no database is ever in —
    /// and would pass while the untracked branch stayed unreachable.
    /// </remarks>
    [Fact]
    public async Task Untracked_Schema_Is_Rejected()
    {
        await using AppDbContext db = _fixture.CreateContext();

        string? original = await ReadSchemaCommentAsync(db);

        try
        {
            await SetSchemaCommentAsync(db, "standard public schema");

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SchemaBootstrapper.EnsureSchemaAsync(db, CancellationToken.None));

            Assert.Contains("predates schema-hash tracking", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("standard public schema", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            await SetSchemaCommentAsync(db, original ?? string.Empty);
        }
    }

    /// <summary>
    /// A caller that owns its database must get the schema rebuilt, not an exception.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// This is the path the test fixture itself takes. It is asserted here because when it
    /// regressed, every integration test in the suite failed at once with a message telling a
    /// human to go and drop a database.
    /// </remarks>
    [Fact]
    public async Task Stale_Schema_Is_Rebuilt_When_The_Caller_Owns_The_Database()
    {
        await using AppDbContext db = _fixture.CreateContext();

        await SetSchemaCommentAsync(db, "standard public schema");

        bool rebuilt = await SchemaBootstrapper.EnsureSchemaAsync(db, CancellationToken.None, resetIfStale: true);

        Assert.True(rebuilt, "A stale schema should have been rebuilt.");

        // And the rebuild must leave the database in a state the strict check accepts.
        bool created = await SchemaBootstrapper.EnsureSchemaAsync(db, CancellationToken.None);
        Assert.False(created);
    }

    /// <summary>Reads the comment recorded on the <c>public</c> schema.</summary>
    /// <param name="db">The context to read through.</param>
    /// <returns>The comment, or <see langword="null"/> when none is set.</returns>
    private static async Task<string?> ReadSchemaCommentAsync(AppDbContext db)
    {
        List<string?> rows = await db.Database
            .SqlQueryRaw<string?>(
                "SELECT obj_description(n.oid, 'pg_namespace') AS \"Value\" " +
                "FROM pg_namespace n WHERE n.nspname = 'public'")
            .ToListAsync(CancellationToken.None);

        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Sets the comment on the <c>public</c> schema.</summary>
    /// <param name="db">The context to write through.</param>
    /// <param name="comment">The comment to set; empty removes it.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task SetSchemaCommentAsync(AppDbContext db, string comment)
    {
        string sql = comment.Length == 0
            ? "COMMENT ON SCHEMA public IS NULL"
            : string.Concat("COMMENT ON SCHEMA public IS '", comment.Replace("'", "''", StringComparison.Ordinal), "'");

        await db.Database.ExecuteSqlRawAsync(sql, CancellationToken.None);
    }

    /// <summary>
    /// Constructing a context without the model assemblies must be impossible rather than
    /// merely discouraged.
    /// </summary>
    [Fact]
    public void ModelAssemblies_Are_Required()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;

        Assert.Throws<ArgumentNullException>(() => new AppDbContext(options, null!));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Prism.Common.Database;

namespace Prism.Tests.Integration;

/// <summary>
/// Guards against the two schema failure modes found in the Phase 0 audit: a model built from
/// an incomplete assembly set, and migrations drifting from the model.
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
    /// The committed migrations must fully describe the current model. When they do not,
    /// migration throws at startup — and Prism
    /// previously reported that as "is PostgreSQL running?".
    /// </summary>
    [Fact]
    public void Migrations_Have_No_Pending_Model_Changes()
    {
        using AppDbContext db = _fixture.CreateContext();

        bool hasPending = db.Database.HasPendingModelChanges();

        Assert.False(
            hasPending,
            "The EF model has changed without a corresponding migration. " +
            "Run: dotnet ef migrations add <Name> --project src/Prism.Common --startup-project src/Prism.Api");
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

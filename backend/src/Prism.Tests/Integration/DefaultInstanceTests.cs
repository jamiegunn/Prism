using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Application.SetDefaultInstance;
using Prism.Features.Models.Application.UnregisterInstance;
using Prism.Features.Models.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers there always being a default instance while there is any instance at all.
/// </summary>
/// <remarks>
/// <para>
/// Deleting the default removed the row and nothing else, leaving every remaining instance with
/// <c>IsDefault = false</c> and the application with no default at all. That is not a cosmetic
/// gap: embedding resolution, batch inference and the evaluation runner all pick their server by
/// "the default first, then …", and with no default they fall through to a tiebreak that was
/// never meant to decide anything — which is how a fresh install ended up embedding against an
/// offline vLLM.
/// </para>
/// <para>
/// Deleting the last instance is different: there is nothing to promote, and no default is the
/// honest state.
/// </para>
/// </remarks>
[Collection("Database")]
public sealed class DefaultInstanceTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultInstanceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public DefaultInstanceTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Deleting the default promotes another instance rather than leaving none.
    /// </summary>
    [Fact]
    public async Task Deleting_The_Default_Promotes_Another()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        Guid defaultId = await SeedAsync(db, "the-default", isDefault: true, status: InstanceStatus.Offline);

        // The offline one is created *first*, so "promote the oldest" and "promote a reachable
        // one" point at different rows. With the online one older, both rules agree and the test
        // could not tell which was in force.
        await SeedAsync(db, "an-offline-one", isDefault: false, status: InstanceStatus.Offline);
        Guid onlineId = await SeedAsync(db, "an-online-one", isDefault: false, status: InstanceStatus.Online);

        Result result = await Handler(db).HandleAsync(
            new UnregisterInstanceCommand(defaultId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        List<InferenceInstance> remaining = await db.Set<InferenceInstance>().AsNoTracking().ToListAsync();

        InferenceInstance promoted = Assert.Single(remaining, i => i.IsDefault);

        // The reachable one, because a default that cannot answer is the problem being avoided.
        Assert.Equal(onlineId, promoted.Id);
    }

    /// <summary>
    /// Deleting a non-default leaves the existing default alone.
    /// </summary>
    [Fact]
    public async Task Deleting_A_Non_Default_Changes_Nothing()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        Guid defaultId = await SeedAsync(db, "the-default", isDefault: true, status: InstanceStatus.Online);
        Guid otherId = await SeedAsync(db, "another", isDefault: false, status: InstanceStatus.Online);

        await Handler(db).HandleAsync(new UnregisterInstanceCommand(otherId), CancellationToken.None);

        InferenceInstance remaining = Assert.Single(
            await db.Set<InferenceInstance>().AsNoTracking().ToListAsync());

        Assert.Equal(defaultId, remaining.Id);
        Assert.True(remaining.IsDefault);
    }

    /// <summary>
    /// Deleting the only instance leaves no default, because there is nothing to promote.
    /// </summary>
    [Fact]
    public async Task Deleting_The_Last_Instance_Leaves_None()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        Guid onlyId = await SeedAsync(db, "the-only-one", isDefault: true, status: InstanceStatus.Online);

        Result result = await Handler(db).HandleAsync(
            new UnregisterInstanceCommand(onlyId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Empty(await db.Set<InferenceInstance>().AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// The default can be moved to another instance.
    /// </summary>
    /// <remarks>
    /// It could only be set at registration — no endpoint offered to change it and no control
    /// asked. With two servers registered there was no way to choose between them short of
    /// deleting one and adding it back.
    /// </remarks>
    [Fact]
    public async Task The_Default_Can_Be_Moved_To_Another_Instance()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        Guid wasDefault = await SeedAsync(db, "was-default", isDefault: true, status: InstanceStatus.Online);
        Guid wanted = await SeedAsync(db, "the-one-i-want", isDefault: false, status: InstanceStatus.Online);

        Result<InferenceInstanceDto> result = await new SetDefaultInstanceHandler(
            db, NullLogger<SetDefaultInstanceHandler>.Instance)
            .HandleAsync(new SetDefaultInstanceCommand(wanted), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        List<InferenceInstance> all = await db.Set<InferenceInstance>().AsNoTracking().ToListAsync();

        // Exactly one, because every consumer orders by the flag and takes the first — two would
        // make that choice arbitrary.
        InferenceInstance promoted = Assert.Single(all, i => i.IsDefault);
        Assert.Equal(wanted, promoted.Id);
        Assert.False(all.Single(i => i.Id == wasDefault).IsDefault);
    }

    /// <summary>
    /// Promoting an instance that does not exist is a not-found, and moves nothing.
    /// </summary>
    [Fact]
    public async Task Promoting_An_Unknown_Instance_Changes_Nothing()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        Guid existing = await SeedAsync(db, "untouched", isDefault: true, status: InstanceStatus.Online);

        Result<InferenceInstanceDto> result = await new SetDefaultInstanceHandler(
            db, NullLogger<SetDefaultInstanceHandler>.Instance)
            .HandleAsync(new SetDefaultInstanceCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);

        InferenceInstance still = Assert.Single(await db.Set<InferenceInstance>().AsNoTracking().ToListAsync());
        Assert.Equal(existing, still.Id);
        Assert.True(still.IsDefault);
    }

    private static UnregisterInstanceHandler Handler(AppDbContext db)
        => new(db, NullLogger<UnregisterInstanceHandler>.Instance);

    private static async Task ClearAsync(AppDbContext db)
        => await db.Set<InferenceInstance>().ExecuteDeleteAsync();

    private static async Task<Guid> SeedAsync(
        AppDbContext db, string name, bool isDefault, InstanceStatus status)
    {
        var instance = new InferenceInstance
        {
            Name = $"{name}-{Guid.NewGuid():N}",
            Endpoint = $"http://localhost:{Random.Shared.Next(9000, 9999)}",
            ProviderType = InferenceProviderType.Ollama,
            ModelId = "mistral:7b-instruct",
            IsDefault = isDefault,
            Status = status,
        };

        db.Set<InferenceInstance>().Add(instance);
        await db.SaveChangesAsync();
        return instance.Id;
    }
}

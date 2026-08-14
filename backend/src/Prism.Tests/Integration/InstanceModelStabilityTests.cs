using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.Models.Application;
using Prism.Features.Models.Application.CheckHealth;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Domain;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Proofs that a health check reports on an instance without quietly rewriting it.
/// </summary>
/// <remarks>
/// <para>
/// The health check refreshed <c>ModelId</c> from the server on every pass, and for Ollama the
/// server's answer was "the model pulled most recently". A background poll every thirty seconds
/// therefore reassigned the model of every registered instance whenever anything new was
/// downloaded — including an embedding model, which cannot chat. Pulling
/// <c>nomic-embed-text</c> so that RAG would work took Playground, Prompt Lab, Agents and the
/// Token Explorer down together, each reporting
/// <c>"nomic-embed-text:latest" does not support chat</c>.
/// </para>
/// <para>
/// A health check answers "is this reachable and what can it do". Which model an instance runs
/// is the user's choice, made at registration or through a model swap, and a poll is not the
/// place to overrule it.
/// </para>
/// </remarks>
[Collection("Database")]
public sealed class InstanceModelStabilityTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceModelStabilityTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public InstanceModelStabilityTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// A server holding three models, newest first: an embedding model, then two that can chat.
    /// </summary>
    /// <remarks>
    /// The third exists so a test can configure an instance to a model the server has but would
    /// never volunteer. Configuring it to the model the server does volunteer would pass whether
    /// or not the choice is protected, and prove nothing.
    /// </remarks>
    private const string TagsEmbeddingFirst = """
        {
          "models": [
            { "name": "nomic-embed-text:latest", "model": "nomic-embed-text:latest",
              "details": { "family": "nomic-bert" } },
            { "name": "mistral:7b-instruct", "model": "mistral:7b-instruct",
              "details": { "family": "llama" } },
            { "name": "qwen2.5:0.5b", "model": "qwen2.5:0.5b",
              "details": { "family": "qwen2" } }
          ]
        }
        """;

    /// <summary>
    /// A configured model survives a health check that finds something newer on the server.
    /// </summary>
    [Fact]
    public async Task A_Health_Check_Leaves_A_Configured_Model_Alone()
    {
        await using AppDbContext db = _fixture.CreateContext();

        // Deliberately the model the server would not pick for itself, so the assertion can only
        // pass by the choice being honoured.
        Guid instanceId = await SeedInstanceAsync(db, "qwen2.5:0.5b");

        Result<InferenceInstanceDto> result = await CreateHandler(db).HandleAsync(
            new CheckHealthCommand(instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("qwen2.5:0.5b", await ModelOfAsync(db, instanceId));
    }

    /// <summary>
    /// An instance registered without a model still gets one filled in.
    /// </summary>
    /// <remarks>
    /// The point is not to freeze <c>ModelId</c> forever — an instance with nothing recorded has
    /// no choice to protect, and leaving it blank would show an empty model everywhere.
    /// </remarks>
    [Fact]
    public async Task A_Health_Check_Fills_In_A_Missing_Model()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedInstanceAsync(db, null);

        await CreateHandler(db).HandleAsync(new CheckHealthCommand(instanceId), CancellationToken.None);

        // Not the embedding model that heads the list: a model that can hold a conversation.
        Assert.Equal("mistral:7b-instruct", await ModelOfAsync(db, instanceId));
    }

    /// <summary>
    /// A model the server no longer has is replaced, because keeping it points at nothing.
    /// </summary>
    [Fact]
    public async Task A_Health_Check_Replaces_A_Model_The_Server_No_Longer_Has()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedInstanceAsync(db, "llama3.1:8b-that-was-deleted");

        await CreateHandler(db).HandleAsync(new CheckHealthCommand(instanceId), CancellationToken.None);

        Assert.Equal("mistral:7b-instruct", await ModelOfAsync(db, instanceId));
    }

    /// <summary>
    /// An instance already pointing at an embedding model is repaired rather than preserved.
    /// </summary>
    /// <remarks>
    /// Protecting the stored model must not mean protecting a broken one. Every install that ran
    /// while the old health check was in place has instances pointing at whatever was pulled
    /// last, and for the installs that pulled an embedding model to get RAG working that value
    /// can never serve a chat. It is not a choice anyone made, so there is nothing to defend —
    /// and without this the fix would leave every existing install exactly as broken as before.
    /// </remarks>
    [Fact]
    public async Task A_Health_Check_Repairs_An_Instance_Left_On_An_Embedding_Model()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedInstanceAsync(db, "nomic-embed-text:latest");

        await CreateHandler(db).HandleAsync(new CheckHealthCommand(instanceId), CancellationToken.None);

        Assert.Equal("mistral:7b-instruct", await ModelOfAsync(db, instanceId));
    }

    /// <summary>
    /// Capabilities are still refreshed, which is what the health check is for.
    /// </summary>
    /// <remarks>
    /// Guards the obvious over-correction: skipping the model must not skip the block that
    /// records what the server can do, or logprobs support would freeze at its registered value.
    /// </remarks>
    [Fact]
    public async Task A_Health_Check_Still_Refreshes_Status_And_Capabilities()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedInstanceAsync(db, "mistral:7b-instruct", online: false);

        Result<InferenceInstanceDto> result = await CreateHandler(db).HandleAsync(
            new CheckHealthCommand(instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        InferenceInstance instance = await db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == instanceId);

        Assert.Equal(InstanceStatus.Online, instance.Status);
        Assert.True(instance.SupportsStreaming);
        Assert.NotNull(instance.LastHealthCheck);
    }

    private static async Task<string?> ModelOfAsync(AppDbContext db, Guid instanceId)
        => await db.Set<InferenceInstance>()
            .AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => i.ModelId)
            .FirstAsync();

    private static CheckHealthHandler CreateHandler(AppDbContext db)
    {
        // Routes are tried in order, and a fragment matches the URL or the body — which is how
        // /api/show, whose model travels in the body, can answer differently per model.
        FakeHttpTransport transport = FakeHttpTransport.JsonByPath(
            ("/api/tags", TagsEmbeddingFirst),
            ("nomic-embed-text", """{ "capabilities": ["embedding"] }"""),
            ("/api/show", """{ "capabilities": ["completion", "tools"] }"""),
            ("/api/version", """{ "version": "0.32.6" }"""),
            ("http", "Ollama is running"));

        return new CheckHealthHandler(
            db,
            new InferenceProviderFactory(transport, NullLoggerFactory.Instance),
            NullLogger<CheckHealthHandler>.Instance);
    }

    private static async Task<Guid> SeedInstanceAsync(
        AppDbContext db, string? modelId, bool online = true)
    {
        var instance = new InferenceInstance
        {
            Name = $"health-target-{Guid.NewGuid():N}",
            Endpoint = "http://localhost:9999",
            ProviderType = InferenceProviderType.Ollama,
            ModelId = modelId,
            Status = online ? InstanceStatus.Online : InstanceStatus.Offline,
        };

        db.Set<InferenceInstance>().Add(instance);
        await db.SaveChangesAsync();
        return instance.Id;
    }
}

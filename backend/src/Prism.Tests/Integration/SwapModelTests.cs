using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.Models.Application;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Application.SwapModel;
using Prism.Features.Models.Domain;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers changing the model an instance runs — the one write the models screen makes.
/// </summary>
/// <remarks>
/// <para>
/// Every way of getting it wrong ended with the instance recorded as running something it was
/// not. An empty model id came back as a 503 carrying Ollama's own <c>invalid model name</c>,
/// which reads as a server fault rather than an empty field. A model that does not exist came
/// back <c>200 OK</c> and was stored, because a failed pull streams its error inside a 200
/// response. And an embedding model was accepted like any other, leaving an instance that cannot
/// hold a conversation — the exact state the health check spends its time repairing.
/// </para>
/// <para>
/// In all three the screen went green and the damage showed up later, somewhere else.
/// </para>
/// </remarks>
[Collection("Database")]
public sealed class SwapModelTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwapModelTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public SwapModelTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>Two chat models and an embedding model, as the server lists them.</summary>
    private const string Tags = """
        {
          "models": [
            { "name": "mistral:7b-instruct", "model": "mistral:7b-instruct",
              "size": 4372824384, "details": { "family": "llama" } },
            { "name": "qwen2.5:0.5b", "model": "qwen2.5:0.5b",
              "size": 397821319, "details": { "family": "qwen2" } },
            { "name": "nomic-embed-text:latest", "model": "nomic-embed-text:latest",
              "size": 274302450, "details": { "family": "nomic-bert" } }
          ]
        }
        """;

    private const string PullSucceeds = """{"status":"success"}""";

    /// <summary>
    /// A blank model is the caller's mistake, and is refused before anything is contacted.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_Blank_Model_Is_A_Validation_Error(string? blank)
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedAsync(db);

        Result<InferenceInstanceDto> result = await Handler(db).HandleAsync(
            new SwapModelCommand(instanceId, blank!), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);

        // The message for an empty box, not the one for a model the server does not have. Both
        // are validation errors and either would stop the write, but "'x' has no model called ''"
        // describes the caller's mistake as though they had named something.
        Assert.Equal("Name the model to switch to.", result.Error.Message);

        // And nothing was changed on the way to finding out.
        Assert.Equal("mistral:7b-instruct", await ModelOfAsync(db, instanceId));
    }

    /// <summary>
    /// A model the server does not have is refused, and the instance keeps the one it had.
    /// </summary>
    [Fact]
    public async Task A_Model_The_Server_Does_Not_Have_Is_Refused()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedAsync(db);

        Result<InferenceInstanceDto> result = await Handler(db).HandleAsync(
            new SwapModelCommand(instanceId, "totally-made-up-model:99b"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("totally-made-up-model:99b", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal("mistral:7b-instruct", await ModelOfAsync(db, instanceId));
    }

    /// <summary>
    /// An embedding model is refused, because an instance is asked to hold conversations.
    /// </summary>
    /// <remarks>
    /// Allowing it produced precisely the breakage the health check now repairs: a registered
    /// instance whose model answers every chat with "does not support chat". Refusing it at the
    /// point of choosing is where the message is useful.
    /// </remarks>
    [Fact]
    public async Task An_Embedding_Model_Is_Refused()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedAsync(db);

        Result<InferenceInstanceDto> result = await Handler(db).HandleAsync(
            new SwapModelCommand(instanceId, "nomic-embed-text:latest"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("embedding", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("mistral:7b-instruct", await ModelOfAsync(db, instanceId));
    }

    /// <summary>
    /// A chat model the server has is accepted, and stored.
    /// </summary>
    [Fact]
    public async Task A_Chat_Model_The_Server_Has_Is_Accepted()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedAsync(db);

        Result<InferenceInstanceDto> result = await Handler(db).HandleAsync(
            new SwapModelCommand(instanceId, "qwen2.5:0.5b"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("qwen2.5:0.5b", await ModelOfAsync(db, instanceId));
    }

    private static async Task<string?> ModelOfAsync(AppDbContext db, Guid instanceId)
        => await db.Set<InferenceInstance>()
            .AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => i.ModelId)
            .FirstAsync();

    private static SwapModelHandler Handler(AppDbContext db)
    {
        FakeHttpTransport transport = FakeHttpTransport.JsonByPath(
            ("/api/tags", Tags),
            ("nomic-embed-text", """{ "capabilities": ["embedding"] }"""),
            ("/api/show", """{ "capabilities": ["completion"] }"""),
            ("/api/pull", PullSucceeds),
            ("http", "Ollama is running"));

        return new SwapModelHandler(
            db,
            new InferenceProviderFactory(transport, NullLoggerFactory.Instance),
            NullLogger<SwapModelHandler>.Instance);
    }

    private static async Task<Guid> SeedAsync(AppDbContext db)
    {
        var instance = new InferenceInstance
        {
            Name = $"swap-target-{Guid.NewGuid():N}",
            Endpoint = "http://localhost:9999",
            ProviderType = InferenceProviderType.Ollama,
            ModelId = "mistral:7b-instruct",
            Status = InstanceStatus.Online,
        };

        db.Set<InferenceInstance>().Add(instance);
        await db.SaveChangesAsync();
        return instance.Id;
    }
}

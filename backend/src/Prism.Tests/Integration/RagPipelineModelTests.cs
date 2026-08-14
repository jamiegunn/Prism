using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Application.RagPipeline;
using Prism.Features.Rag.Domain;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers which model the RAG answer step is generated with.
/// </summary>
/// <remarks>
/// The pipeline resolved the instance to call and then sent <c>command.Model</c> straight
/// through, so a request that named an instance but no model reached Ollama with an empty one
/// and came back <c>{"error":"model is required"}</c> as a 503 — a message about a field the
/// caller had every reason to think the instance supplied. Retrieval worked, generation did not,
/// which made the failure look like the inference server rather than the request.
/// </remarks>
[Collection("Database")]
public sealed class RagPipelineModelTests
{
    private const int Dimensions = 4;

    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagPipelineModelTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public RagPipelineModelTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// With no model named, the answer is generated with the instance's own model.
    /// </summary>
    [Fact]
    public async Task An_Unnamed_Model_Falls_Back_To_The_Instance()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedCollectionAsync(db);
        Guid instanceId = await SeedInstanceAsync(db, "mistral:7b-instruct");

        FakeHttpTransport transport = FakeHttpTransport.ChatCompletion("Attention weighs the input.");

        Result<RagPipelineResultDto> result = await CreateHandler(db, transport).HandleAsync(
            Command(collectionId, instanceId, model: ""), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Contains("mistral:7b-instruct", transport.RequestBodies[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A model named explicitly is the one used.
    /// </summary>
    [Fact]
    public async Task A_Named_Model_Is_Used_As_Given()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedCollectionAsync(db);
        Guid instanceId = await SeedInstanceAsync(db, "mistral:7b-instruct");

        FakeHttpTransport transport = FakeHttpTransport.ChatCompletion("An answer.");

        Result<RagPipelineResultDto> result = await CreateHandler(db, transport).HandleAsync(
            Command(collectionId, instanceId, model: "qwen2.5:0.5b"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Contains("qwen2.5:0.5b", transport.RequestBodies[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// An instance with no model of its own fails before the call, saying what is missing.
    /// </summary>
    /// <remarks>
    /// The alternative is what used to happen: an empty model on the wire and the inference
    /// server's own complaint returned as a 503, which reads as "the server is down".
    /// </remarks>
    [Fact]
    public async Task With_No_Model_Anywhere_The_Failure_Names_The_Problem()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedCollectionAsync(db);
        Guid instanceId = await SeedInstanceAsync(db, null);

        FakeHttpTransport transport = FakeHttpTransport.ChatCompletion("never reached");

        Result<RagPipelineResultDto> result = await CreateHandler(db, transport).HandleAsync(
            Command(collectionId, instanceId, model: ""), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("model", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RagPipelineCommand Command(Guid collectionId, Guid instanceId, string model)
        => new(
            collectionId,
            Query: "What is attention?",
            Model: model,
            InstanceId: instanceId,
            SystemPrompt: null,
            PromptTemplate: null,
            TopK: 3,
            SearchType: SearchType.Vector,
            Temperature: null,
            MaxTokens: 64);

    private static RagPipelineHandler CreateHandler(AppDbContext db, FakeHttpTransport transport)
        => new(
            new QueryCollectionHandler(
                db,
                new StubEmbeddingProvider([1f, 0f, 0f, 0f]),
                NullLogger<QueryCollectionHandler>.Instance),
            new InferenceProviderFactory(transport, NullLoggerFactory.Instance),
            db,
            NullLogger<RagPipelineHandler>.Instance);

    private static async Task<Guid> SeedInstanceAsync(AppDbContext db, string? modelId)
    {
        var instance = new InferenceInstance
        {
            Name = $"rag-target-{Guid.NewGuid():N}",
            Endpoint = "http://localhost:9999",
            ProviderType = InferenceProviderType.OpenAiCompatible,
            ModelId = modelId,
        };

        db.Set<InferenceInstance>().Add(instance);
        await db.SaveChangesAsync();
        return instance.Id;
    }

    private static async Task<Guid> SeedCollectionAsync(AppDbContext db)
    {
        var collection = new RagCollection
        {
            Name = $"rag-pipeline-{Guid.NewGuid():N}",
            EmbeddingModel = "stub",
            Dimensions = Dimensions,
        };
        db.Set<RagCollection>().Add(collection);

        var document = new RagDocument
        {
            CollectionId = collection.Id,
            Filename = $"pipeline-{Guid.NewGuid():N}.txt",
            ContentType = "text/plain",
        };
        db.Set<RagDocument>().Add(document);

        db.Set<RagChunk>().Add(new RagChunk
        {
            DocumentId = document.Id,
            Content = "Self-attention weighs every position against every other.",
            Embedding = new Vector(new float[] { 1f, 0f, 0f, 0f }),
            OrderIndex = 0,
            TokenCount = 8,
        });

        await db.SaveChangesAsync();
        return collection.Id;
    }

    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        private readonly float[] _vector;

        public StubEmbeddingProvider(float[] vector) => _vector = vector;

        public Task<Result<float[]>> EmbedAsync(string text, string model, CancellationToken ct)
            => Task.FromResult(Result<float[]>.Success(_vector));

        public Task<Result<IReadOnlyList<float[]>>> EmbedBatchAsync(
            IReadOnlyList<string> texts, string model, CancellationToken ct)
            => Task.FromResult(Result<IReadOnlyList<float[]>>.Success(
                texts.Select(_ => _vector).ToList()));
    }
}

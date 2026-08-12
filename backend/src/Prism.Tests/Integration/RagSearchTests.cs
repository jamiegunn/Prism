using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers vector and hybrid retrieval against a real pgvector database.
/// </summary>
/// <remarks>
/// Written to fail on the pre-fix code, where <c>QueryCollectionHandler</c> called
/// <c>.Include("")</c>. EF Core rejects an empty navigation path, so every vector and
/// hybrid query threw <see cref="ArgumentException"/> before reaching the database —
/// meaning RAG search, and the agent tool built on it, were broken for all callers.
/// </remarks>
[Collection("Database")]
public sealed class RagSearchTests
{
    private const int Dimensions = 4;

    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagSearchTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public RagSearchTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// A vector search must return chunks ranked by similarity to the query embedding.
    /// </summary>
    [Fact]
    public async Task VectorSearch_Returns_Chunks_Ranked_By_Similarity()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedCollectionAsync(db);

        var handler = new QueryCollectionHandler(
            db,
            new StubEmbeddingProvider([1f, 0f, 0f, 0f]),
            NullLogger<QueryCollectionHandler>.Instance);

        Result<List<ChunkSearchResultDto>> result = await handler.HandleAsync(
            new QueryCollectionQuery(collectionId, "anything", TopK: 3, SearchType.Vector),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsSuccess ? "" : $"Vector search failed: {result.Error.Message}");
        Assert.NotEmpty(result.Value);

        // The chunk whose embedding equals the query vector must rank first.
        Assert.Equal("exact match", result.Value[0].Content);

        // Scores must be ordered descending — a search that returns rows in arbitrary
        // order is not a search.
        List<double> scores = result.Value.Select(r => r.Score).ToList();
        Assert.Equal(scores.OrderByDescending(s => s).ToList(), scores);
    }

    /// <summary>
    /// Hybrid search fuses vector and full-text ranking, and must also survive the round trip.
    /// </summary>
    [Fact]
    public async Task HybridSearch_Returns_Results()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedCollectionAsync(db);

        var handler = new QueryCollectionHandler(
            db,
            new StubEmbeddingProvider([1f, 0f, 0f, 0f]),
            NullLogger<QueryCollectionHandler>.Instance);

        Result<List<ChunkSearchResultDto>> result = await handler.HandleAsync(
            new QueryCollectionQuery(collectionId, "exact", TopK: 3, SearchType.Hybrid),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsSuccess ? "" : $"Hybrid search failed: {result.Error.Message}");
    }

    /// <summary>
    /// An empty or whitespace query is a validation error, caught before any embedding call —
    /// it used to reach the provider and surface as a 503, reading as a server outage for what
    /// is really an empty search box. A provider that would throw proves the guard runs first.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task Empty_Query_Is_Rejected_Before_Embedding(string queryText)
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedCollectionAsync(db);

        var handler = new QueryCollectionHandler(
            db,
            new ThrowingEmbeddingProvider(),
            NullLogger<QueryCollectionHandler>.Instance);

        Result<List<ChunkSearchResultDto>> result = await handler.HandleAsync(
            new QueryCollectionQuery(collectionId, queryText, TopK: 3, SearchType.Vector),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    private sealed class ThrowingEmbeddingProvider : IEmbeddingProvider
    {
        public Task<Result<float[]>> EmbedAsync(string text, string model, CancellationToken ct) =>
            throw new InvalidOperationException("The empty-query guard should run before embedding.");

        public Task<Result<IReadOnlyList<float[]>>> EmbedBatchAsync(
            IReadOnlyList<string> texts, string model, CancellationToken ct) =>
            throw new InvalidOperationException("The empty-query guard should run before embedding.");
    }

    private static async Task<Guid> SeedCollectionAsync(AppDbContext db)
    {
        var collection = new RagCollection
        {
            Name = $"test-{Guid.NewGuid():N}",
            EmbeddingModel = "stub",
            Dimensions = Dimensions,
        };
        db.Set<RagCollection>().Add(collection);

        var document = new RagDocument
        {
            CollectionId = collection.Id,
            Filename = "doc.txt",
            ContentType = "text/plain",
        };
        db.Set<RagDocument>().Add(document);

        (string Content, float[] Embedding)[] chunks =
        [
            ("exact match", [1f, 0f, 0f, 0f]),
            ("partial match", [0.8f, 0.2f, 0f, 0f]),
            ("unrelated", [0f, 0f, 0f, 1f]),
        ];

        for (int i = 0; i < chunks.Length; i++)
        {
            db.Set<RagChunk>().Add(new RagChunk
            {
                DocumentId = document.Id,
                Content = chunks[i].Content,
                Embedding = new Vector(chunks[i].Embedding),
                OrderIndex = i,
                TokenCount = 2,
            });
        }

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

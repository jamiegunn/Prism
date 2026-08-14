using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Application.QuerySets;
using Prism.Features.Rag.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// End-to-end proofs for retrieval evaluation against a real pgvector database: the metrics
/// computed over the app's own retrieval match hand-computed values, per-mode failure is
/// reported as failure (not zeros), and labels are validated against the collection.
/// </summary>
/// <remarks>
/// The seeded demo collection ships with null embeddings, so these tests build their own
/// embedded fixture — the trap called out in the plan.
/// </remarks>
[Collection("Database")]
public sealed class RetrievalEvaluationTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalEvaluationTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public RetrievalEvaluationTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Vector mode over a fixture whose ranking is fully determined by the embeddings:
    /// query embeds to [1,0,0,0]; chunks A=[1,0,0,0], B=[0.8,0.2,0,0](normalized differs),
    /// C=[0,0,0,1]. Ranking by cosine: A, B, C. Relevant = {A}:
    /// precision@1 = 1, precision@3 = 1/3, recall@1 = 1, mrr = 1, ndcg@3 = 1.
    /// Relevant = {C} for the second query (same ranking): first relevant at rank 3 →
    /// precision@1 = 0, mrr = 1/3, ndcg@3 = 1/log2(4) = 0.5.
    /// Means over the two queries: precision@1 = 0.5, mrr = (1 + 1/3)/2 = 2/3,
    /// ndcg@3 = (1 + 0.5)/2 = 0.75.
    /// </summary>
    [Fact]
    public async Task Vector_Mode_Metrics_Match_Hand_Computed_Values()
    {
        await using AppDbContext db = _fixture.CreateContext();
        (Guid collectionId, Guid[] chunkIds) = await SeedEmbeddedCollectionAsync(db);

        var queryHandler = new QueryCollectionHandler(
            db, new StubEmbeddingProvider([1f, 0f, 0f, 0f]),
            NullLogger<QueryCollectionHandler>.Instance);

        var createHandler = new CreateQuerySetHandler(db);
        Result<RagQuerySetDto> created = await createHandler.HandleAsync(
            new CreateQuerySetCommand(collectionId, "labels", null,
            [
                ("find A", [chunkIds[0]]),
                ("find C", [chunkIds[2]]),
            ]),
            CancellationToken.None);

        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Message : "");

        var evaluate = new EvaluateRetrievalHandler(
            db, queryHandler, NullLogger<EvaluateRetrievalHandler>.Instance);

        Result<RetrievalEvaluationDto> result = await evaluate.HandleAsync(
            new EvaluateRetrievalCommand(
                collectionId, created.Value.Id, TopK: 3, [SearchType.Vector]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : "");

        RetrievalModeResultDto vector = Assert.Single(result.Value.Modes);
        Assert.Equal("vector", vector.Mode);
        Assert.Null(vector.Error);
        Assert.NotNull(vector.Metrics);
        Assert.Equal(2, vector.QueryCount);

        Assert.Equal(0.5, vector.Metrics!["precision@1"], 12);
        Assert.Equal(2.0 / 3.0, vector.Metrics["mrr"], 12);
        Assert.Equal(0.75, vector.Metrics["ndcg@3"], 12);
        Assert.Equal(0.5, vector.Metrics["recall@1"], 12); // (1 + 0)/2

        // Definitions ride along with the numbers.
        Assert.Contains("precision@k", result.Value.Definitions.Keys);
        Assert.Contains("ndcg@3", result.Value.Definitions.Keys);
    }

    /// <summary>
    /// A collection with no embeddings must report vector and hybrid as unavailable — with
    /// the reason — rather than scoring them zero, while BM25 still evaluates.
    /// </summary>
    [Fact]
    public async Task Unembedded_Collection_Reports_Vector_Unavailable_Not_Zero()
    {
        await using AppDbContext db = _fixture.CreateContext();
        (Guid collectionId, Guid[] chunkIds) = await SeedEmbeddedCollectionAsync(db, embed: false);

        var queryHandler = new QueryCollectionHandler(
            db, new StubEmbeddingProvider([1f, 0f, 0f, 0f]),
            NullLogger<QueryCollectionHandler>.Instance);

        var createHandler = new CreateQuerySetHandler(db);
        Result<RagQuerySetDto> created = await createHandler.HandleAsync(
            new CreateQuerySetCommand(collectionId, "labels", null,
                [("unique-alpha content", [chunkIds[0]])]),
            CancellationToken.None);

        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Message : "");

        var evaluate = new EvaluateRetrievalHandler(
            db, queryHandler, NullLogger<EvaluateRetrievalHandler>.Instance);

        Result<RetrievalEvaluationDto> result = await evaluate.HandleAsync(
            new EvaluateRetrievalCommand(collectionId, created.Value.Id, 5, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : "");
        Assert.Equal(3, result.Value.Modes.Count);

        RetrievalModeResultDto vector = result.Value.Modes.Single(m => m.Mode == "vector");
        Assert.Null(vector.Metrics);
        Assert.NotNull(vector.Error);
        Assert.Contains("embedding", vector.Error, StringComparison.OrdinalIgnoreCase);

        RetrievalModeResultDto hybrid = result.Value.Modes.Single(m => m.Mode == "hybrid");
        Assert.Null(hybrid.Metrics);
        Assert.NotNull(hybrid.Error);

        // BM25 needs no embeddings: it must evaluate, and with content "unique-alpha content"
        // matching chunk A exactly, precision@1 = 1.
        RetrievalModeResultDto bm25 = result.Value.Modes.Single(m => m.Mode == "bm25");
        Assert.Null(bm25.Error);
        Assert.NotNull(bm25.Metrics);
        Assert.Equal(1.0, bm25.Metrics!["precision@1"], 12);
    }

    /// <summary>
    /// A label pointing at a chunk from another collection is rejected at creation — it
    /// would silently score every retrieval as a miss.
    /// </summary>
    [Fact]
    public async Task Labels_Must_Belong_To_The_Collection()
    {
        await using AppDbContext db = _fixture.CreateContext();
        (Guid collectionA, Guid[] chunksA) = await SeedEmbeddedCollectionAsync(db);
        (Guid collectionB, Guid[] chunksB) = await SeedEmbeddedCollectionAsync(db);

        var createHandler = new CreateQuerySetHandler(db);

        Result<RagQuerySetDto> crossLabel = await createHandler.HandleAsync(
            new CreateQuerySetCommand(collectionA, "bad", null,
                [("query", [chunksB[0]])]),
            CancellationToken.None);

        Assert.True(crossLabel.IsFailure);
        Assert.Equal(ErrorType.Validation, crossLabel.Error.Type);

        // And an unlabelled item is rejected too.
        Result<RagQuerySetDto> emptyLabels = await createHandler.HandleAsync(
            new CreateQuerySetCommand(collectionA, "bad2", null, [("query", [])]),
            CancellationToken.None);

        Assert.True(emptyLabels.IsFailure);

        // Sanity: a correct set on collection A is accepted.
        Result<RagQuerySetDto> good = await createHandler.HandleAsync(
            new CreateQuerySetCommand(collectionA, "good", null, [("query", [chunksA[0]])]),
            CancellationToken.None);

        Assert.True(good.IsSuccess, good.IsFailure ? good.Error.Message : "");
    }

    /// <summary>
    /// A set sent with no items at all is the caller's mistake, not the server's.
    /// </summary>
    /// <remarks>
    /// Omitting <c>items</c> — or sending <c>"items": null</c> — reached
    /// <c>request.Items.Select(...)</c> and came back as a 500 reading
    /// <c>Value cannot be null. (Parameter 'source')</c>, which tells the caller nothing about
    /// the field they left out. An empty array was already a clean 400; null was not.
    /// </remarks>
    [Fact]
    public async Task A_Query_Set_With_No_Items_Is_A_Validation_Error()
    {
        await using AppDbContext db = _fixture.CreateContext();
        (Guid collectionId, Guid[] _) = await SeedEmbeddedCollectionAsync(db);

        var createHandler = new CreateQuerySetHandler(db);

        Result<RagQuerySetDto> nullItems = await createHandler.HandleAsync(
            new CreateQuerySetCommand(collectionId, "no items", null, null!),
            CancellationToken.None);

        Assert.True(nullItems.IsFailure);
        Assert.Equal(ErrorType.Validation, nullItems.Error.Type);
    }

    /// <summary>
    /// An item whose labelled-chunk list is null is rejected the way an empty one is.
    /// </summary>
    [Fact]
    public async Task An_Item_With_Null_Labels_Is_A_Validation_Error()
    {
        await using AppDbContext db = _fixture.CreateContext();
        (Guid collectionId, Guid[] _) = await SeedEmbeddedCollectionAsync(db);

        Result<RagQuerySetDto> result = await new CreateQuerySetHandler(db).HandleAsync(
            new CreateQuerySetCommand(collectionId, "null labels", null, [("query", null!)]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    /// <summary>
    /// Seeds a collection with three chunks whose contents are lexically distinct (for BM25)
    /// and whose embeddings produce the deterministic ranking A, B, C for query [1,0,0,0].
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="embed">Whether to store embeddings; false reproduces the seeded-demo
    /// null-embedding state.</param>
    /// <returns>The collection id and the chunk ids in content order A, B, C.</returns>
    private static async Task<(Guid CollectionId, Guid[] ChunkIds)> SeedEmbeddedCollectionAsync(
        AppDbContext db, bool embed = true)
    {
        var collection = new RagCollection
        {
            Name = $"eval-{Guid.NewGuid():N}",
            EmbeddingModel = "stub",
            Dimensions = 4,
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
            ("unique-alpha content", [1f, 0f, 0f, 0f]),
            ("unique-bravo content", [0.8f, 0.2f, 0f, 0f]),
            ("unique-charlie content", [0f, 0f, 0f, 1f]),
        ];

        var ids = new Guid[chunks.Length];

        for (int i = 0; i < chunks.Length; i++)
        {
            var chunk = new RagChunk
            {
                DocumentId = document.Id,
                Content = chunks[i].Content,
                Embedding = embed ? new Vector(chunks[i].Embedding) : null,
                OrderIndex = i,
                TokenCount = 2,
            };
            db.Set<RagChunk>().Add(chunk);
            ids[i] = chunk.Id;
        }

        await db.SaveChangesAsync();
        return (collection.Id, ids);
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

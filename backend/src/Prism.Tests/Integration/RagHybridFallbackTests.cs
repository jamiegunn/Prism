using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Application.QuerySets;
using Prism.Features.Rag.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers what hybrid search does when it can only run half of itself.
/// </summary>
/// <remarks>
/// <para>
/// Hybrid used to fail outright the moment embedding was unavailable, throwing away the BM25 half
/// it had already computed — so "no embedding server" became "no results at all", on a page whose
/// keyword mode was working perfectly one dropdown away.
/// </para>
/// <para>
/// Returning that half silently would have been the worse fix. A result labelled hybrid that is
/// really keyword-only is a claim about method, and method is the entire subject of a retrieval
/// comparison. So the half comes back with a statement that it is a half, and the retrieval
/// evaluation — whose job is to score methods — refuses to score it at all.
/// </para>
/// </remarks>
[Collection("Database")]
public sealed class RagHybridFallbackTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagHybridFallbackTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public RagHybridFallbackTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// With embedding unavailable, hybrid returns its keyword half rather than nothing.
    /// </summary>
    [Fact]
    public async Task Hybrid_Falls_Back_To_Keyword_Results()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedAsync(db);

        Result<ChunkSearchOutcomeDto> result = await Handler(db, embeddingWorks: false).HandleAsync(
            new QueryCollectionQuery(collectionId, "transformer", TopK: 3, SearchType.Hybrid),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.NotEmpty(result.Value.Results);
    }

    /// <summary>
    /// …and says so, naming the half that ran and why the other did not.
    /// </summary>
    [Fact]
    public async Task The_Fallback_Says_It_Is_Not_Hybrid()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedAsync(db);

        Result<ChunkSearchOutcomeDto> result = await Handler(db, embeddingWorks: false).HandleAsync(
            new QueryCollectionQuery(collectionId, "transformer", TopK: 3, SearchType.Hybrid),
            CancellationToken.None);

        Assert.False(result.Value.RanAsRequested);
        Assert.Contains("BM25", result.Value.DegradedReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("embedding-unavailable", result.Value.DegradedReason!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A hybrid search that really ran both halves claims nothing.
    /// </summary>
    [Fact]
    public async Task A_Real_Hybrid_Search_Reports_No_Degradation()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedAsync(db);

        Result<ChunkSearchOutcomeDto> result = await Handler(db, embeddingWorks: true).HandleAsync(
            new QueryCollectionQuery(collectionId, "transformer", TopK: 3, SearchType.Hybrid),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.True(result.Value.RanAsRequested);
        Assert.Null(result.Value.DegradedReason);
    }

    /// <summary>
    /// Vector alone still fails, because there is no half of it to fall back to.
    /// </summary>
    [Fact]
    public async Task Vector_Alone_Still_Fails()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedAsync(db);

        Result<ChunkSearchOutcomeDto> result = await Handler(db, embeddingWorks: false).HandleAsync(
            new QueryCollectionQuery(collectionId, "transformer", TopK: 3, SearchType.Vector),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    /// <summary>
    /// The retrieval evaluation refuses to score a mode that did not run as asked.
    /// </summary>
    /// <remarks>
    /// The point of the whole degradation being visible. Scoring the keyword half as "hybrid"
    /// would put a number against a method that never ran — a false comparison, which is the one
    /// failure a retrieval evaluation must never produce.
    /// </remarks>
    [Fact]
    public async Task The_Evaluation_Will_Not_Score_A_Fallback_As_Hybrid()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedAsync(db);

        Guid[] chunkIds = [.. await ChunkIdsAsync(db, collectionId)];

        var createHandler = new CreateQuerySetHandler(db);
        Result<RagQuerySetDto> created = await createHandler.HandleAsync(
            new CreateQuerySetCommand(collectionId, "labels", null, [("transformer", [chunkIds[0]])]),
            CancellationToken.None);

        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Message : null);

        var evaluate = new EvaluateRetrievalHandler(
            db, Handler(db, embeddingWorks: false), NullLogger<EvaluateRetrievalHandler>.Instance);

        Result<RetrievalEvaluationDto> result = await evaluate.HandleAsync(
            new EvaluateRetrievalCommand(collectionId, created.Value.Id, TopK: 3, [SearchType.Hybrid]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        RetrievalModeResultDto hybrid = Assert.Single(result.Value.Modes);
        Assert.Equal("hybrid", hybrid.Mode);
        Assert.Null(hybrid.Metrics);
        Assert.NotNull(hybrid.Error);
    }

    private static QueryCollectionHandler Handler(AppDbContext db, bool embeddingWorks)
        => new(
            db,
            new SometimesEmbedding(embeddingWorks),
            NullLogger<QueryCollectionHandler>.Instance);

    private static async Task<List<Guid>> ChunkIdsAsync(AppDbContext db, Guid collectionId)
    {
        List<Guid> documentIds = await db.Set<RagDocument>()
            .Where(d => d.CollectionId == collectionId)
            .Select(d => d.Id)
            .ToListAsync();

        return await db.Set<RagChunk>()
            .Where(c => documentIds.Contains(c.DocumentId))
            .OrderBy(c => c.OrderIndex)
            .Select(c => c.Id)
            .ToListAsync();
    }

    private static async Task<Guid> SeedAsync(AppDbContext db)
    {
        var collection = new RagCollection
        {
            Name = $"hybrid-{Guid.NewGuid():N}",
            EmbeddingModel = "stub",
            Dimensions = 4,
        };
        db.Set<RagCollection>().Add(collection);

        var document = new RagDocument
        {
            CollectionId = collection.Id,
            Filename = $"hybrid-{Guid.NewGuid():N}.txt",
            ContentType = "text/plain",
        };
        db.Set<RagDocument>().Add(document);

        // Both mention "transformer", so BM25 has something to find with no embedding at all.
        (string Content, float[] Embedding)[] chunks =
        [
            ("the transformer architecture replaced recurrence", [1f, 0f, 0f, 0f]),
            ("a transformer layer stacks attention and feed-forward blocks", [0.9f, 0.1f, 0f, 0f]),
        ];

        for (int i = 0; i < chunks.Length; i++)
        {
            db.Set<RagChunk>().Add(new RagChunk
            {
                DocumentId = document.Id,
                Content = chunks[i].Content,
                Embedding = new Vector(chunks[i].Embedding),
                OrderIndex = i,
                TokenCount = 8,
            });
        }

        await db.SaveChangesAsync();
        return collection.Id;
    }

    /// <summary>An embedding provider that can be switched off, the way an absent server is.</summary>
    private sealed class SometimesEmbedding : IEmbeddingProvider
    {
        private readonly bool _works;

        public SometimesEmbedding(bool works) => _works = works;

        public Task<Result<float[]>> EmbedAsync(string text, string model, CancellationToken ct)
            => Task.FromResult(_works
                ? Result<float[]>.Success([1f, 0f, 0f, 0f])
                : Result<float[]>.Failure(Error.Unavailable("embedding-unavailable")));

        public Task<Result<IReadOnlyList<float[]>>> EmbedBatchAsync(
            IReadOnlyList<string> texts, string model, CancellationToken ct)
            => Task.FromResult(_works
                ? Result<IReadOnlyList<float[]>>.Success(texts.Select(_ => new[] { 1f, 0f, 0f, 0f }).ToList())
                : Result<IReadOnlyList<float[]>>.Failure(Error.Unavailable("embedding-unavailable")));
    }
}

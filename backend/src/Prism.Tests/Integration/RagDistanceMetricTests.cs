using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers the distance metric a collection is created with actually being used.
/// </summary>
/// <remarks>
/// <para>
/// Cosine, Euclidean and Inner Product were offered when creating a collection, stored on the
/// row, and then never read: every search ranked by cosine. A collection built to compare
/// metrics compared nothing, and the setting was a control that moved without connecting to
/// anything — the worst kind, because the result looks like an answer.
/// </para>
/// <para>
/// The three vectors below are chosen so that each metric has a *different* winner. Cosine reads
/// only the angle, so it prefers the tiny vector pointing exactly at the query. Euclidean reads
/// the distance, so it prefers the one that lands nearest. Inner product reads angle and
/// magnitude together, so it prefers the long one. Any test where two metrics agree would pass
/// against the old always-cosine code and prove nothing.
/// </para>
/// </remarks>
[Collection("Database")]
public sealed class RagDistanceMetricTests
{
    /// <summary>Query direction all three are measured against.</summary>
    private static readonly float[] Query = [1f, 0f, 0f, 0f];

    /// <summary>
    /// Cosine ranks by angle alone, so a vector of any length pointing exactly at the query wins.
    /// </summary>
    [Fact]
    public async Task Cosine_Ranks_By_Angle()
        => await AssertTopResultAsync(DistanceMetricType.Cosine, "tiny-but-exactly-aligned");

    /// <summary>
    /// Euclidean ranks by how far apart the points are, so the nearest one wins.
    /// </summary>
    [Fact]
    public async Task Euclidean_Ranks_By_Distance()
        => await AssertTopResultAsync(DistanceMetricType.Euclidean, "nearest-in-space");

    /// <summary>
    /// Inner product rewards magnitude as well as agreement, so the long vector wins.
    /// </summary>
    [Fact]
    public async Task Inner_Product_Rewards_Magnitude()
        => await AssertTopResultAsync(DistanceMetricType.InnerProduct, "long-and-roughly-aligned");

    /// <summary>
    /// Scores still descend, whichever metric produced them.
    /// </summary>
    /// <remarks>
    /// Each metric reports a different quantity, and Euclidean's is a distance — smaller is
    /// better. Returning it raw would leave the list ordered best-first while its numbers climbed,
    /// so the column would contradict the ranking beside it.
    /// </remarks>
    [Theory]
    [InlineData(DistanceMetricType.Cosine)]
    [InlineData(DistanceMetricType.Euclidean)]
    [InlineData(DistanceMetricType.InnerProduct)]
    public async Task Scores_Descend_For_Every_Metric(DistanceMetricType metric)
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedAsync(db, metric);

        List<ChunkSearchResultDto> results = await SearchAsync(db, collectionId);

        List<double> scores = [.. results.Select(r => r.Score)];
        Assert.Equal([.. scores.OrderByDescending(s => s)], scores);
    }

    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagDistanceMetricTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public RagDistanceMetricTests(DatabaseFixture fixture) => _fixture = fixture;

    private async Task AssertTopResultAsync(DistanceMetricType metric, string expectedTop)
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid collectionId = await SeedAsync(db, metric);

        List<ChunkSearchResultDto> results = await SearchAsync(db, collectionId);

        Assert.NotEmpty(results);
        Assert.Equal(expectedTop, results[0].Content);
    }

    private static async Task<List<ChunkSearchResultDto>> SearchAsync(AppDbContext db, Guid collectionId)
    {
        var handler = new QueryCollectionHandler(
            db,
            new FixedEmbedding(Query),
            NullLogger<QueryCollectionHandler>.Instance);

        Result<ChunkSearchOutcomeDto> result = await handler.HandleAsync(
            new QueryCollectionQuery(collectionId, "anything", TopK: 3, SearchType.Vector),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value.Results;
    }

    private static async Task<Guid> SeedAsync(AppDbContext db, DistanceMetricType metric)
    {
        var collection = new RagCollection
        {
            Name = $"metric-{metric}-{Guid.NewGuid():N}",
            EmbeddingModel = "stub",
            Dimensions = 4,
            DistanceMetric = metric,
        };
        db.Set<RagCollection>().Add(collection);

        var document = new RagDocument
        {
            CollectionId = collection.Id,
            Filename = $"metric-{Guid.NewGuid():N}.txt",
            ContentType = "text/plain",
        };
        db.Set<RagDocument>().Add(document);

        (string Content, float[] Embedding)[] chunks =
        [
            // cosine 1.00000 | L2 0.99000 | inner 0.010
            ("tiny-but-exactly-aligned", [0.01f, 0f, 0f, 0f]),
            // cosine 0.94868 | L2 2.23607 | inner 3.000
            ("long-and-roughly-aligned", [3f, 1f, 0f, 0f]),
            // cosine 0.99887 | L2 0.07071 | inner 1.050
            ("nearest-in-space", [1.05f, 0.05f, 0f, 0f]),
        ];

        for (int i = 0; i < chunks.Length; i++)
        {
            db.Set<RagChunk>().Add(new RagChunk
            {
                DocumentId = document.Id,
                Content = chunks[i].Content,
                Embedding = new Vector(chunks[i].Embedding),
                OrderIndex = i,
                TokenCount = 3,
            });
        }

        await db.SaveChangesAsync();
        return collection.Id;
    }

    private sealed class FixedEmbedding : IEmbeddingProvider
    {
        private readonly float[] _vector;

        public FixedEmbedding(float[] vector) => _vector = vector;

        public Task<Result<float[]>> EmbedAsync(string text, string model, CancellationToken ct)
            => Task.FromResult(Result<float[]>.Success(_vector));

        public Task<Result<IReadOnlyList<float[]>>> EmbedBatchAsync(
            IReadOnlyList<string> texts, string model, CancellationToken ct)
            => Task.FromResult(Result<IReadOnlyList<float[]>>.Success(
                texts.Select(_ => _vector).ToList()));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Domain;
using Prism.Features.Rag.Infrastructure;

namespace Prism.Tests.Integration;

/// <summary>
/// Proofs that the sample RAG collection can actually be searched.
/// </summary>
/// <remarks>
/// <para>
/// The seeder wrote three chunks with <c>Embedding = null</c> and then marked the document
/// <c>Completed</c> and the collection <c>Ready</c>. Vector search filters on
/// <c>Embedding != null</c>, so the only collection a new user has returned nothing for every
/// semantic query while presenting itself as finished — the feature's headline mode, dead on
/// arrival, reported as healthy. BM25 worked, which made it look like a relevance problem
/// rather than an empty index.
/// </para>
/// <para>
/// Embedding at seed time needs a provider that may not be up yet, so the two outcomes both
/// have to be honest: embedded and ready, or not embedded and not claiming to be.
/// </para>
/// </remarks>
[Collection("Database")]
public sealed class RagSeedEmbeddingTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagSeedEmbeddingTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public RagSeedEmbeddingTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// With a working provider, the sample is embedded and says it is ready.
    /// </summary>
    [Fact]
    public async Task The_Sample_Collection_Is_Seeded_With_Embeddings()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        await new RagSeeder(Embedder(new StubEmbedder(Working: true)))
            .SeedAsync(db, CancellationToken.None);

        Assert.Equal(3, await EmbeddedChunkCountAsync(db));

        RagDocument document = await db.Set<RagDocument>().AsNoTracking().SingleAsync();
        Assert.Equal(DocumentProcessingStatus.Completed, document.Status);

        RagCollection collection = await db.Set<RagCollection>().AsNoTracking().SingleAsync();
        Assert.Equal(RagCollectionStatus.Ready, collection.Status);
    }

    /// <summary>
    /// With no provider reachable, the sample does not claim to be ready.
    /// </summary>
    /// <remarks>
    /// The seed still runs — the text and its chunks are worth having, and BM25 can search them.
    /// What must not survive is the <c>Completed</c> badge, which is the part that turned an
    /// empty vector index into a relevance mystery.
    /// </remarks>
    [Fact]
    public async Task A_Sample_That_Could_Not_Be_Embedded_Does_Not_Claim_To_Be_Ready()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        await new RagSeeder(Embedder(new StubEmbedder(Working: false)))
            .SeedAsync(db, CancellationToken.None);

        Assert.Equal(0, await EmbeddedChunkCountAsync(db));

        RagDocument document = await db.Set<RagDocument>().AsNoTracking().SingleAsync();
        Assert.NotEqual(DocumentProcessingStatus.Completed, document.Status);
        Assert.False(string.IsNullOrWhiteSpace(document.ErrorMessage));
    }

    /// <summary>
    /// A later run embeds a sample that was seeded before the provider worked.
    /// </summary>
    /// <remarks>
    /// Without this, every install that ever started before its embedding model was pulled keeps
    /// a permanently unsearchable sample: the seeder skips a database that already has a
    /// collection, so the broken one is never revisited. The repair matters more than the
    /// first-run path, since that is the state real installs are in.
    /// </remarks>
    [Fact]
    public async Task A_Later_Run_Embeds_A_Sample_Seeded_Without_Embeddings()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        await new RagSeeder(Embedder(new StubEmbedder(Working: false)))
            .SeedAsync(db, CancellationToken.None);

        Assert.Equal(0, await EmbeddedChunkCountAsync(db));

        await new RagSeeder(Embedder(new StubEmbedder(Working: true)))
            .SeedAsync(db, CancellationToken.None);

        Assert.Equal(3, await EmbeddedChunkCountAsync(db));

        RagDocument document = await db.Set<RagDocument>().AsNoTracking().SingleAsync();
        Assert.Equal(DocumentProcessingStatus.Completed, document.Status);
        Assert.True(string.IsNullOrWhiteSpace(document.ErrorMessage));
    }

    /// <summary>
    /// A second run does not seed a second sample, and does not re-embed what is already done.
    /// </summary>
    [Fact]
    public async Task Seeding_Twice_Leaves_One_Sample_And_Embeds_Once()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        var embedder = new StubEmbedder(Working: true);

        await new RagSeeder(Embedder(embedder)).SeedAsync(db, CancellationToken.None);
        await new RagSeeder(Embedder(embedder)).SeedAsync(db, CancellationToken.None);

        Assert.Equal(1, await db.Set<RagCollection>().CountAsync());
        Assert.Equal(3, await EmbeddedChunkCountAsync(db));
        Assert.Equal(3, embedder.TextsEmbedded);
    }

    /// <summary>
    /// A user's own collection is never touched by the repair.
    /// </summary>
    /// <remarks>
    /// The backfill hunts for unembedded chunks, and the obvious over-reach is to embed every
    /// one it finds — spending calls on collections the user built deliberately, possibly with a
    /// different model and dimension than the one it would use.
    /// </remarks>
    [Fact]
    public async Task The_Repair_Leaves_Other_Collections_Alone()
    {
        await using AppDbContext db = _fixture.CreateContext();
        await ClearAsync(db);

        // The sample is embedded first, so the user's collection is the only unembedded thing
        // left. A backfill that hunts for "a document with missing vectors" rather than for the
        // sample would therefore land squarely on it — which is the mistake being ruled out.
        await new RagSeeder(Embedder(new StubEmbedder(Working: true)))
            .SeedAsync(db, CancellationToken.None);

        Guid mineId = await SeedForeignCollectionAsync(db);

        await new RagSeeder(Embedder(new StubEmbedder(Working: true)))
            .SeedAsync(db, CancellationToken.None);

        List<RagChunk> mine = await db.Set<RagChunk>()
            .AsNoTracking()
            .Where(c => db.Set<RagDocument>()
                .Where(d => d.CollectionId == mineId)
                .Select(d => d.Id)
                .Contains(c.DocumentId))
            .ToListAsync();

        Assert.All(mine, c => Assert.Null(c.Embedding));
    }

    private static RagSampleEmbedder Embedder(IEmbeddingProvider provider)
        => new(provider, NullLogger<RagSampleEmbedder>.Instance);

    private static async Task<int> EmbeddedChunkCountAsync(AppDbContext db)
        => await db.Set<RagChunk>().AsNoTracking().CountAsync(c => c.Embedding != null);

    private static async Task ClearAsync(AppDbContext db)
    {
        await db.Set<RagChunk>().ExecuteDeleteAsync();
        await db.Set<RagDocument>().ExecuteDeleteAsync();
        await db.Set<RagCollection>().ExecuteDeleteAsync();
    }

    private static async Task<Guid> SeedForeignCollectionAsync(AppDbContext db)
    {
        Guid collectionId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        db.Set<RagCollection>().Add(new RagCollection
        {
            Id = collectionId,
            Name = "My Own Papers",
            EmbeddingModel = "some-other-model",
            Dimensions = 4,
            DistanceMetric = DistanceMetricType.Cosine,
            ChunkingStrategy = "recursive",
            ChunkSize = 512,
            ChunkOverlap = 50,
            Status = RagCollectionStatus.Ready,
            Documents =
            [
                new RagDocument
                {
                    Id = documentId,
                    CollectionId = collectionId,
                    Filename = "mine.txt",
                    ContentType = "text/plain",
                    SizeBytes = 10,
                    ChunkCount = 1,
                    CharacterCount = 10,
                    Status = DocumentProcessingStatus.Pending,
                    Chunks =
                    [
                        new RagChunk
                        {
                            DocumentId = documentId,
                            Content = "my own text",
                            Embedding = null,
                            OrderIndex = 0,
                            TokenCount = 3,
                            StartOffset = 0,
                            EndOffset = 10,
                        }
                    ],
                }
            ],
        });

        await db.SaveChangesAsync();
        return collectionId;
    }

    /// <summary>An embedder that either works or is unreachable, counting what it embedded.</summary>
    private sealed class StubEmbedder : IEmbeddingProvider
    {
        private readonly bool _working;

        public StubEmbedder(bool Working) => _working = Working;

        public int TextsEmbedded { get; private set; }

        public Task<Result<float[]>> EmbedAsync(string text, string model, CancellationToken ct)
            => Task.FromResult(_working
                ? Result<float[]>.Success(Vector())
                : Result<float[]>.Failure(Error.Unavailable("Embedding request failed: NotFound")));

        public Task<Result<IReadOnlyList<float[]>>> EmbedBatchAsync(
            IReadOnlyList<string> texts, string model, CancellationToken ct)
        {
            if (!_working)
            {
                return Task.FromResult(Result<IReadOnlyList<float[]>>.Failure(
                    Error.Unavailable("Embedding request failed: NotFound")));
            }

            TextsEmbedded += texts.Count;

            return Task.FromResult(Result<IReadOnlyList<float[]>>.Success(
                texts.Select(_ => Vector()).ToList()));
        }

        // 768 wide to match the dimension the sample collection declares.
        private static float[] Vector() => [.. Enumerable.Repeat(0.1f, 768)];
    }
}

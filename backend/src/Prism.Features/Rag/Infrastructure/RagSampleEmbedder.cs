using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Prism.Common.Results;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Infrastructure;

/// <summary>
/// Embeds the seeded sample collection, so that it can be searched by meaning and not only by
/// keyword.
/// </summary>
/// <remarks>
/// Separate from the seeder because the two moments it is needed are different. Seeding runs
/// before anything has been health-checked, so on a first launch there may be no reachable
/// inference server yet; the same work has to be attempted again once there is. Sharing one
/// implementation is what keeps "seeded fresh" and "repaired later" from drifting into two
/// different definitions of a ready sample.
/// </remarks>
public sealed class RagSampleEmbedder
{
    /// <summary>The filename that identifies the seeded sample document.</summary>
    public const string SampleFilename = "attention-is-all-you-need-summary.txt";

    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<RagSampleEmbedder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagSampleEmbedder"/> class.
    /// </summary>
    /// <param name="embeddingProvider">The provider used to embed the sample chunks.</param>
    /// <param name="logger">The logger instance.</param>
    public RagSampleEmbedder(IEmbeddingProvider embeddingProvider, ILogger<RagSampleEmbedder> logger)
    {
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    /// <summary>
    /// Embeds the sample document's chunks when they have no vectors yet.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Scoped to the seeded sample by filename rather than to every unembedded chunk in the
    /// database. A user's own collection may be mid-ingest, may use a different model, or may
    /// have been left unembedded on purpose; none of that is the seeder's to decide, and
    /// embedding it would spend calls against a model whose dimensions need not even match.
    /// </remarks>
    public async Task EmbedIfNeededAsync(AppDbContext context, CancellationToken ct)
    {
        RagDocument? document = await context.Set<RagDocument>()
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Filename == SampleFilename, ct);

        if (document is null)
        {
            return;
        }

        List<RagChunk> unembedded = [.. document.Chunks.Where(c => c.Embedding is null)];

        if (unembedded.Count == 0)
        {
            return;
        }

        RagCollection? collection = await context.Set<RagCollection>()
            .FirstOrDefaultAsync(c => c.Id == document.CollectionId, ct);

        if (collection is null)
        {
            return;
        }

        Result<IReadOnlyList<float[]>> embeddings = await _embeddingProvider.EmbedBatchAsync(
            [.. unembedded.Select(c => c.Content)], collection.EmbeddingModel, ct);

        if (embeddings.IsFailure)
        {
            // Stored where the workbench shows it, because the previous silence sent people
            // looking at their query wording instead. The reason is repeated verbatim rather
            // than explained: seeding runs before the first health check, so a failure here is
            // as often an inference server that is not up yet as a model that is not pulled,
            // and naming the wrong one sends people to fix something that was never broken.
            document.Status = DocumentProcessingStatus.Pending;
            document.ErrorMessage =
                $"Not embedded yet: {embeddings.Error.Message}. Keyword (BM25) search works; " +
                "search by meaning needs embeddings. This usually clears on the next start once " +
                $"an inference server is up with the '{collection.EmbeddingModel}' model.";

            collection.Status = RagCollectionStatus.Indexing;

            await context.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Sample RAG collection left unembedded: {Reason}", embeddings.Error.Message);

            return;
        }

        for (int i = 0; i < unembedded.Count; i++)
        {
            unembedded[i].Embedding = new Vector(embeddings.Value[i]);
        }

        // The declared width is what the collection was seeded with; the real width is what the
        // model returned. Storing the guess over the fact is how a collection ends up unable to
        // explain why its own vectors do not fit.
        collection.Dimensions = embeddings.Value[0].Length;

        document.Status = DocumentProcessingStatus.Completed;
        document.ErrorMessage = null;
        collection.Status = RagCollectionStatus.Ready;

        await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Embedded {ChunkCount} sample RAG chunks with {Model}",
            unembedded.Count, collection.EmbeddingModel);
    }
}

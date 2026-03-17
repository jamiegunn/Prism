using Microsoft.EntityFrameworkCore;
using Prism.Common.Database.Seeders;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Infrastructure;

/// <summary>
/// Seeds a sample RAG collection with a document and chunks to demonstrate
/// the RAG Workbench feature on first launch.
/// </summary>
public sealed class RagSeeder : IDataSeeder
{
    /// <summary>
    /// Gets the execution order. RAG seeds at order 150, after datasets.
    /// </summary>
    public int Order => 150;

    /// <summary>
    /// Seeds a sample RAG collection if none exist.
    /// Creates an "AI Research Papers" collection with one document and three text chunks
    /// summarizing the Transformer architecture paper.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasCollections = await context.Set<RagCollection>().AnyAsync(ct);

        if (hasCollections)
        {
            return;
        }

        Guid collectionId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        var collection = new RagCollection
        {
            Id = collectionId,
            Name = "AI Research Papers",
            Description = "Sample collection for testing RAG retrieval",
            EmbeddingModel = "nomic-embed-text",
            Dimensions = 768,
            DistanceMetric = DistanceMetricType.Cosine,
            ChunkingStrategy = "recursive",
            ChunkSize = 512,
            ChunkOverlap = 50,
            DocumentCount = 1,
            ChunkCount = 3,
            Status = RagCollectionStatus.Ready,
            Documents =
            [
                new RagDocument
                {
                    Id = documentId,
                    CollectionId = collectionId,
                    Filename = "attention-is-all-you-need-summary.txt",
                    ContentType = "text/plain",
                    SizeBytes = 1850,
                    ChunkCount = 3,
                    CharacterCount = 1850,
                    Status = DocumentProcessingStatus.Completed,
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = "arxiv",
                        ["year"] = "2017",
                        ["topic"] = "transformers"
                    },
                    Chunks =
                    [
                        new RagChunk
                        {
                            DocumentId = documentId,
                            Content = "The Transformer architecture, introduced in 'Attention Is All You Need' by Vaswani et al. " +
                                      "(2017), revolutionized natural language processing by replacing recurrent neural networks " +
                                      "with a purely attention-based mechanism. The key innovation is the self-attention mechanism, " +
                                      "which allows the model to weigh the importance of different parts of the input when producing " +
                                      "each output element.",
                            Embedding = null,
                            OrderIndex = 0,
                            TokenCount = 62,
                            StartOffset = 0,
                            EndOffset = 385
                        },
                        new RagChunk
                        {
                            DocumentId = documentId,
                            Content = "The architecture consists of an encoder-decoder structure. The encoder maps an input " +
                                      "sequence to a sequence of continuous representations. The decoder then generates an output " +
                                      "sequence one element at a time. Both the encoder and decoder use stacked self-attention and " +
                                      "point-wise fully connected layers. Multi-head attention allows the model to jointly attend " +
                                      "to information from different representation subspaces.",
                            Embedding = null,
                            OrderIndex = 1,
                            TokenCount = 65,
                            StartOffset = 335,
                            EndOffset = 755
                        },
                        new RagChunk
                        {
                            DocumentId = documentId,
                            Content = "The Transformer achieved state-of-the-art results on machine translation benchmarks, " +
                                      "reaching 28.4 BLEU on the WMT 2014 English-to-German translation task. Training is " +
                                      "significantly more parallelizable than previous sequence-to-sequence models, reducing " +
                                      "training time substantially. The architecture has since become the foundation for large " +
                                      "language models including GPT, BERT, and their successors.",
                            Embedding = null,
                            OrderIndex = 2,
                            TokenCount = 60,
                            StartOffset = 705,
                            EndOffset = 1105
                        }
                    ]
                }
            ]
        };

        context.Set<RagCollection>().Add(collection);
        await context.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Domain;
using Prism.Features.Rag.Domain.Chunking;
using Prism.Features.Rag.Domain.Parsing;

namespace Prism.Features.Rag.Application.IngestDocument;

/// <summary>
/// Command to ingest a document into a RAG collection.
/// </summary>
public sealed record IngestDocumentCommand(
    Guid CollectionId,
    string Filename,
    Stream Content,
    long SizeBytes,
    string ContentType);

/// <summary>
/// Handles document ingestion: parsing, chunking, embedding, and storage.
/// </summary>
public sealed class IngestDocumentHandler
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly IEnumerable<IChunkingStrategy> _chunkers;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<IngestDocumentHandler> _logger;

    private const int EmbeddingBatchSize = 32;

    /// <summary>
    /// Initializes a new instance of the <see cref="IngestDocumentHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="parsers">Registered document parsers.</param>
    /// <param name="chunkers">Registered chunking strategies.</param>
    /// <param name="embeddingProvider">The embedding provider.</param>
    /// <param name="logger">The logger instance.</param>
    public IngestDocumentHandler(
        AppDbContext db,
        IEnumerable<IDocumentParser> parsers,
        IEnumerable<IChunkingStrategy> chunkers,
        IEmbeddingProvider embeddingProvider,
        ILogger<IngestDocumentHandler> logger)
    {
        _db = db;
        _parsers = parsers;
        _chunkers = chunkers;
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    /// <summary>
    /// Ingests a document into the specified collection.
    /// </summary>
    /// <param name="command">The ingestion command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the document DTO.</returns>
    public async Task<Result<RagDocumentDto>> HandleAsync(IngestDocumentCommand command, CancellationToken ct)
    {
        RagCollection? collection = await _db.Set<RagCollection>()
            .FirstOrDefaultAsync(c => c.Id == command.CollectionId, ct);

        if (collection is null)
            return Error.NotFound($"RAG collection {command.CollectionId} not found.");

        // Resolve content type from filename if generic
        string contentType = ResolveContentType(command.ContentType, command.Filename);

        // Find parser
        IDocumentParser? parser = _parsers.FirstOrDefault(p =>
            p.SupportedContentTypes.Any(t => t.Equals(contentType, StringComparison.OrdinalIgnoreCase)));

        if (parser is null)
            return Error.Validation($"Unsupported content type: {contentType}. Supported: txt, md, html.");

        // Create document record
        var document = new RagDocument
        {
            CollectionId = command.CollectionId,
            Filename = command.Filename,
            ContentType = contentType,
            SizeBytes = command.SizeBytes,
            Status = DocumentProcessingStatus.Processing
        };

        _db.Set<RagDocument>().Add(document);
        await _db.SaveChangesAsync(ct);

        try
        {
            // Parse document
            string text = await parser.ParseAsync(command.Content, command.Filename, ct);
            document.CharacterCount = text.Length;

            // Chunk text
            IChunkingStrategy chunker = _chunkers.FirstOrDefault(c =>
                c.Name.Equals(collection.ChunkingStrategy, StringComparison.OrdinalIgnoreCase))
                ?? _chunkers.First(c => c.Name == "recursive");

            List<TextChunk> textChunks = chunker.Chunk(text, collection.ChunkSize, collection.ChunkOverlap);

            if (textChunks.Count == 0)
            {
                document.Status = DocumentProcessingStatus.Completed;
                document.ChunkCount = 0;
                await _db.SaveChangesAsync(ct);
                return RagDocumentDto.FromEntity(document);
            }

            // Create chunk entities
            var chunks = new List<RagChunk>();
            for (int i = 0; i < textChunks.Count; i++)
            {
                chunks.Add(new RagChunk
                {
                    DocumentId = document.Id,
                    Content = textChunks[i].Content,
                    OrderIndex = i,
                    TokenCount = textChunks[i].Content.Length / 4, // rough estimate
                    StartOffset = textChunks[i].StartOffset,
                    EndOffset = textChunks[i].EndOffset
                });
            }

            // Generate embeddings in batches
            for (int i = 0; i < chunks.Count; i += EmbeddingBatchSize)
            {
                List<RagChunk> batch = chunks.Skip(i).Take(EmbeddingBatchSize).ToList();
                List<string> texts = batch.Select(c => c.Content).ToList();

                Result<IReadOnlyList<float[]>> embedResult = await _embeddingProvider.EmbedBatchAsync(
                    texts, collection.EmbeddingModel, ct);

                if (embedResult.IsFailure)
                {
                    document.Status = DocumentProcessingStatus.Failed;
                    document.ErrorMessage = $"Embedding failed: {embedResult.Error.Message}";
                    await _db.SaveChangesAsync(ct);
                    return Error.Internal(document.ErrorMessage);
                }

                for (int j = 0; j < batch.Count; j++)
                {
                    batch[j].Embedding = new Vector(embedResult.Value[j]);
                }
            }

            // Save chunks
            _db.Set<RagChunk>().AddRange(chunks);

            // Update document and collection stats
            document.ChunkCount = chunks.Count;
            document.Status = DocumentProcessingStatus.Completed;
            collection.DocumentCount += 1;
            collection.ChunkCount += chunks.Count;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Ingested document {Filename} into collection {CollectionName}: {ChunkCount} chunks",
                document.Filename, collection.Name, chunks.Count);

            return RagDocumentDto.FromEntity(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            document.Status = DocumentProcessingStatus.Failed;
            document.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(ct);

            _logger.LogError(ex, "Failed to ingest document {Filename}", command.Filename);
            return Error.Internal($"Document ingestion failed: {ex.Message}");
        }
    }

    private static string ResolveContentType(string contentType, string filename)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType != "application/octet-stream")
            return contentType;

        string ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".md" or ".markdown" => "text/markdown",
            ".html" or ".htm" => "text/html",
            _ => "text/plain"
        };
    }
}

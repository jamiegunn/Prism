namespace Prism.Features.Rag.Domain.Chunking;

/// <summary>
/// Defines a strategy for splitting document text into chunks for embedding.
/// </summary>
public interface IChunkingStrategy
{
    /// <summary>
    /// Gets the unique name of this chunking strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Splits the input text into chunks with the specified size and overlap.
    /// </summary>
    /// <param name="text">The full document text to chunk.</param>
    /// <param name="chunkSize">The target chunk size in characters.</param>
    /// <param name="chunkOverlap">The overlap between adjacent chunks in characters.</param>
    /// <returns>A list of text chunks with their start/end offsets.</returns>
    List<TextChunk> Chunk(string text, int chunkSize, int chunkOverlap);
}

/// <summary>
/// Represents a single chunk of text with its position in the source document.
/// </summary>
/// <param name="Content">The text content of the chunk.</param>
/// <param name="StartOffset">The character offset where this chunk starts in the original text.</param>
/// <param name="EndOffset">The character offset where this chunk ends in the original text.</param>
public sealed record TextChunk(string Content, int StartOffset, int EndOffset);

namespace Prism.Features.Rag.Domain.Chunking;

/// <summary>
/// Splits text into fixed-size chunks with configurable overlap.
/// </summary>
public sealed class FixedSizeChunker : IChunkingStrategy
{
    /// <inheritdoc />
    public string Name => "fixed";

    /// <inheritdoc />
    public List<TextChunk> Chunk(string text, int chunkSize, int chunkOverlap)
    {
        var chunks = new List<TextChunk>();
        if (string.IsNullOrEmpty(text) || chunkSize <= 0)
            return chunks;

        int step = Math.Max(1, chunkSize - chunkOverlap);
        int offset = 0;

        while (offset < text.Length)
        {
            int end = Math.Min(offset + chunkSize, text.Length);
            string content = text[offset..end];
            chunks.Add(new TextChunk(content, offset, end));

            if (end >= text.Length)
                break;

            offset += step;
        }

        return chunks;
    }
}

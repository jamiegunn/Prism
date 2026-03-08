using System.Text.RegularExpressions;

namespace Prism.Features.Rag.Domain.Chunking;

/// <summary>
/// Recursively splits text using a hierarchy of separators: paragraphs, then sentences, then fixed-size.
/// This produces more semantically meaningful chunks.
/// </summary>
public sealed partial class RecursiveChunker : IChunkingStrategy
{
    /// <inheritdoc />
    public string Name => "recursive";

    private static readonly string[] Separators = ["\n\n", "\n", ". ", " "];

    [GeneratedRegex(@"\n\n+")]
    private static partial Regex ParagraphSeparator();

    /// <inheritdoc />
    public List<TextChunk> Chunk(string text, int chunkSize, int chunkOverlap)
    {
        var chunks = new List<TextChunk>();
        if (string.IsNullOrEmpty(text) || chunkSize <= 0)
            return chunks;

        RecursiveSplit(text, 0, chunkSize, chunkOverlap, 0, chunks);
        return MergeSmallChunks(chunks, chunkSize, chunkOverlap);
    }

    private static void RecursiveSplit(
        string text,
        int baseOffset,
        int chunkSize,
        int chunkOverlap,
        int separatorIndex,
        List<TextChunk> results)
    {
        if (text.Length <= chunkSize)
        {
            if (text.Length > 0)
                results.Add(new TextChunk(text, baseOffset, baseOffset + text.Length));
            return;
        }

        if (separatorIndex >= Separators.Length)
        {
            // Fall back to fixed-size splitting
            int step = Math.Max(1, chunkSize - chunkOverlap);
            int offset = 0;
            while (offset < text.Length)
            {
                int end = Math.Min(offset + chunkSize, text.Length);
                results.Add(new TextChunk(text[offset..end], baseOffset + offset, baseOffset + end));
                if (end >= text.Length) break;
                offset += step;
            }
            return;
        }

        string separator = Separators[separatorIndex];
        string[] parts = text.Split(separator);

        if (parts.Length <= 1)
        {
            RecursiveSplit(text, baseOffset, chunkSize, chunkOverlap, separatorIndex + 1, results);
            return;
        }

        int currentOffset = baseOffset;
        foreach (string part in parts)
        {
            if (part.Length <= chunkSize)
            {
                if (part.Length > 0)
                    results.Add(new TextChunk(part, currentOffset, currentOffset + part.Length));
            }
            else
            {
                RecursiveSplit(part, currentOffset, chunkSize, chunkOverlap, separatorIndex + 1, results);
            }

            currentOffset += part.Length + separator.Length;
        }
    }

    private static List<TextChunk> MergeSmallChunks(List<TextChunk> chunks, int chunkSize, int chunkOverlap)
    {
        if (chunks.Count <= 1)
            return chunks;

        var merged = new List<TextChunk>();
        TextChunk current = chunks[0];

        for (int i = 1; i < chunks.Count; i++)
        {
            string combined = current.Content + " " + chunks[i].Content;
            if (combined.Length <= chunkSize)
            {
                current = new TextChunk(combined, current.StartOffset, chunks[i].EndOffset);
            }
            else
            {
                merged.Add(current);
                current = chunks[i];
            }
        }

        merged.Add(current);
        return merged;
    }
}

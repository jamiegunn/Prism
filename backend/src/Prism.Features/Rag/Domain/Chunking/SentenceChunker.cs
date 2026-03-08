using System.Text.RegularExpressions;

namespace Prism.Features.Rag.Domain.Chunking;

/// <summary>
/// Splits text at sentence boundaries, grouping sentences to approximate the target chunk size.
/// </summary>
public sealed partial class SentenceChunker : IChunkingStrategy
{
    /// <inheritdoc />
    public string Name => "sentence";

    [GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.Compiled)]
    private static partial Regex SentenceBoundary();

    /// <inheritdoc />
    public List<TextChunk> Chunk(string text, int chunkSize, int chunkOverlap)
    {
        var chunks = new List<TextChunk>();
        if (string.IsNullOrEmpty(text) || chunkSize <= 0)
            return chunks;

        string[] sentences = SentenceBoundary().Split(text);
        var currentChunk = new List<string>();
        int currentLength = 0;
        int currentStart = 0;
        int runningOffset = 0;

        foreach (string sentence in sentences)
        {
            if (currentLength + sentence.Length > chunkSize && currentChunk.Count > 0)
            {
                string content = string.Join(" ", currentChunk);
                chunks.Add(new TextChunk(content, currentStart, currentStart + content.Length));

                // Keep overlap by retaining trailing sentences
                int overlapLength = 0;
                int keepFrom = currentChunk.Count;
                for (int i = currentChunk.Count - 1; i >= 0; i--)
                {
                    if (overlapLength + currentChunk[i].Length > chunkOverlap)
                        break;
                    overlapLength += currentChunk[i].Length + 1;
                    keepFrom = i;
                }

                if (keepFrom < currentChunk.Count)
                {
                    currentChunk = currentChunk.GetRange(keepFrom, currentChunk.Count - keepFrom);
                    currentLength = currentChunk.Sum(s => s.Length) + currentChunk.Count - 1;
                    currentStart = runningOffset - currentLength;
                }
                else
                {
                    currentChunk.Clear();
                    currentLength = 0;
                    currentStart = runningOffset;
                }
            }

            if (currentChunk.Count == 0)
                currentStart = runningOffset;

            currentChunk.Add(sentence);
            currentLength += sentence.Length + (currentChunk.Count > 1 ? 1 : 0);
            runningOffset += sentence.Length + 1; // +1 for the space
        }

        if (currentChunk.Count > 0)
        {
            string content = string.Join(" ", currentChunk);
            chunks.Add(new TextChunk(content, currentStart, currentStart + content.Length));
        }

        return chunks;
    }
}

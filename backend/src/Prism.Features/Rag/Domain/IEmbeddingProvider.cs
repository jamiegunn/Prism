using Prism.Common.Results;

namespace Prism.Features.Rag.Domain;

/// <summary>
/// Provides embedding generation for text inputs, typically backed by an OpenAI-compatible /v1/embeddings endpoint.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates embedding vectors for a batch of text inputs.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="model">The embedding model to use.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the embedding vectors in the same order as the inputs.</returns>
    Task<Result<IReadOnlyList<float[]>>> EmbedBatchAsync(IReadOnlyList<string> texts, string model, CancellationToken ct);

    /// <summary>
    /// Generates an embedding vector for a single text input.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="model">The embedding model to use.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the embedding vector.</returns>
    Task<Result<float[]>> EmbedAsync(string text, string model, CancellationToken ct);
}

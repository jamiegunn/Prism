using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prism.Common.Results;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Infrastructure;

/// <summary>
/// Embedding provider that uses an OpenAI-compatible /v1/embeddings endpoint (e.g., vLLM, OpenAI).
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiEmbeddingProvider> _logger;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiEmbeddingProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenAiEmbeddingProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<OpenAiEmbeddingProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _baseUrl = config["Embedding:BaseUrl"] ?? config["Inference:DefaultEndpoint"] ?? "http://localhost:8000";
    }

    /// <inheritdoc />
    public async Task<Result<float[]>> EmbedAsync(string text, string model, CancellationToken ct)
    {
        Result<IReadOnlyList<float[]>> result = await EmbedBatchAsync([text], model, ct);
        if (result.IsFailure)
            return Result<float[]>.Failure(result.Error);

        return result.Value[0];
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<float[]>>> EmbedBatchAsync(IReadOnlyList<string> texts, string model, CancellationToken ct)
    {
        try
        {
            var request = new EmbeddingRequest(model, texts.ToList());
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl.TrimEnd('/')}/v1/embeddings", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Embedding request failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
                return Error.Unavailable($"Embedding request failed: {response.StatusCode}");
            }

            EmbeddingResponse? embeddingResponse = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
            if (embeddingResponse?.Data is null || embeddingResponse.Data.Count == 0)
                return Error.Internal("Empty embedding response");

            List<float[]> embeddings = embeddingResponse.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToList();

            return Result<IReadOnlyList<float[]>>.Success(embeddings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Embedding request failed for model {Model}", model);
            return Error.Unavailable($"Embedding request failed: {ex.Message}");
        }
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] List<string> Input);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] List<EmbeddingData> Data);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);
}

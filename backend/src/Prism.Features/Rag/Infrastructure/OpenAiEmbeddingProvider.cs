using Prism.Features.Models.Domain;
using Prism.Common.Database;
using Microsoft.EntityFrameworkCore;
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
    private const string DefaultBaseUrl = "http://localhost:8000";

    private readonly string? _configuredBaseUrl;
    private readonly AppDbContext? _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiEmbeddingProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="db">
    /// Optional context used to discover a registered inference endpoint when none is
    /// configured explicitly.
    /// </param>
    public OpenAiEmbeddingProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<OpenAiEmbeddingProvider> logger,
        AppDbContext? db = null)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _db = db;

        // Explicit configuration still wins, but it is no longer the only source. Neither
        // Embedding:BaseUrl nor Inference:DefaultEndpoint exists in appsettings.json, so this
        // silently defaulted to a vLLM address for everyone - an Ollama-only user got no
        // embeddings and no explanation.
        _configuredBaseUrl = config["Embedding:BaseUrl"] ?? config["Inference:DefaultEndpoint"];
    }

    /// <summary>
    /// Resolves the endpoint to embed against: explicit configuration if present, otherwise a
    /// registered inference instance, and only then the historical default.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The base URL to call.</returns>
    private async Task<string> ResolveBaseUrlAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_configuredBaseUrl))
        {
            return _configuredBaseUrl;
        }

        if (_db is not null)
        {
            // Reachable first, then the default, then the oldest.
            //
            // "Default, then oldest" still sent embeddings to a server that was not running. The
            // seeder registers a vLLM and an Ollama with the same timestamp and neither marked
            // default, so the tie broke arbitrarily and landed on the vLLM: on a fresh install
            // the sample collection came up unembedded with `Connection refused
            // (host.docker.internal:8000)` while a healthy Ollama sat one row away. An endpoint
            // that does not answer cannot serve any request, so that is the first question —
            // ahead of a preference that can only be honoured by a server that is up.
            string? endpoint = await _db.Set<InferenceInstance>()
                .AsNoTracking()
                .OrderByDescending(i => i.Status == InstanceStatus.Online)
                .ThenByDescending(i => i.IsDefault)
                .ThenBy(i => i.CreatedAt)
                .Select(i => i.Endpoint)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                return endpoint;
            }
        }

        return DefaultBaseUrl;
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
        string baseUrl = await ResolveBaseUrlAsync(ct);

        try
        {
            var request = new EmbeddingRequest(model, texts.ToList());
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                EmbeddingsUrl(baseUrl), request, ct);

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

    /// <summary>
    /// Builds the embeddings URL for a base address that may or may not already name <c>/v1</c>.
    /// </summary>
    /// <param name="baseUrl">The instance endpoint.</param>
    /// <returns>The absolute URL to POST to.</returns>
    /// <remarks>
    /// vLLM and LM Studio publish their address with the <c>/v1</c> included, and that is how
    /// they get registered. Appending another produced <c>/v1/v1/embeddings</c> and a 404 on
    /// every request, so embeddings could not work against either of them at all. Ollama's
    /// endpoint carries no <c>/v1</c>, which is why the fault survived: it never showed up on
    /// the setup most people run.
    /// </remarks>
    private static string EmbeddingsUrl(string baseUrl)
    {
        string trimmed = baseUrl.TrimEnd('/');

        return trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{trimmed}/embeddings"
            : $"{trimmed}/v1/embeddings";
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

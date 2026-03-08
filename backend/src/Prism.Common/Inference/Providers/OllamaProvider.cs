using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference.Providers;

/// <summary>
/// Inference provider implementation for Ollama's REST API.
/// Ollama uses its own API format (/api/chat, /api/tags, /api/show, /api/pull) rather than
/// the OpenAI-compatible format. Implements <see cref="IHotReloadableProvider"/> for model
/// loading and unloading support.
/// </summary>
public sealed class OllamaProvider : IHotReloadableProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with Ollama's base URL.</param>
    /// <param name="providerName">The display name for this provider instance.</param>
    /// <param name="endpoint">The base endpoint URL of the Ollama server.</param>
    /// <param name="logger">The logger instance.</param>
    public OllamaProvider(
        HttpClient httpClient,
        string providerName,
        string endpoint,
        ILogger<OllamaProvider> logger)
    {
        _httpClient = httpClient;
        ProviderName = providerName;
        Endpoint = endpoint.TrimEnd('/');
        _logger = logger;
    }

    /// <summary>
    /// Gets the display name of this provider instance.
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the base endpoint URL.
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// Gets the Ollama-specific capabilities. Ollama supports streaming and hot-reload,
    /// but has limited logprobs support and no guided decoding or metrics.
    /// </summary>
    public ProviderCapabilities Capabilities { get; } = new()
    {
        SupportsChat = true,
        SupportsStreaming = true,
        SupportsLogprobs = false,
        MaxTopLogprobs = 0,
        SupportsTokenize = false,
        SupportsGuidedDecoding = false,
        SupportsMetrics = false,
        SupportsHotReload = true,
        SupportsSystemMessages = true,
        SupportsFrequencyPenalty = true,
        SupportsPresencePenalty = true,
        SupportsStopSequences = true
    };

    /// <summary>
    /// Sends a chat completion request to Ollama's /api/chat endpoint.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the chat response.</returns>
    public async Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        try
        {
            JsonObject requestBody = BuildOllamaRequestBody(request, stream: false);
            string requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

            using StringContent content = new(requestJson, Encoding.UTF8, "application/json");
            using HttpResponseMessage httpResponse = await _httpClient.PostAsync(
                $"{Endpoint}/api/chat", content, ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError("Ollama chat request failed with status {StatusCode}: {ErrorBody}",
                    (int)httpResponse.StatusCode, errorBody);
                return Error.Unavailable($"Ollama returned {(int)httpResponse.StatusCode}: {errorBody}");
            }

            string responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
            JsonNode? responseNode = JsonNode.Parse(responseJson);

            if (responseNode is null)
            {
                return Error.Internal("Failed to parse Ollama response.");
            }

            string responseContent = responseNode["message"]?["content"]?.GetValue<string>() ?? "";
            bool done = responseNode["done"]?.GetValue<bool>() ?? true;
            string? doneReason = responseNode["done_reason"]?.GetValue<string>();

            int promptTokens = responseNode["prompt_eval_count"]?.GetValue<int>() ?? 0;
            int completionTokens = responseNode["eval_count"]?.GetValue<int>() ?? 0;
            long totalDurationNs = responseNode["total_duration"]?.GetValue<long>() ?? 0;
            long evalDurationNs = responseNode["eval_duration"]?.GetValue<long>() ?? 0;

            double? tokensPerSecond = evalDurationNs > 0
                ? completionTokens / (evalDurationNs / 1_000_000_000.0)
                : null;

            return new ChatResponse
            {
                Content = responseContent,
                FinishReason = doneReason ?? (done ? "stop" : null),
                Usage = new UsageInfo(promptTokens, completionTokens, promptTokens + completionTokens),
                LogprobsData = null,
                ModelId = request.Model,
                Timing = new TimingInfo(
                    LatencyMs: totalDurationNs / 1_000_000,
                    TtftMs: null,
                    TokensPerSecond: tokensPerSecond)
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Connection failed to Ollama at {Endpoint}", Endpoint);
            return Error.Unavailable($"Connection to Ollama failed: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Request to Ollama timed out");
            return Error.Unavailable("Request to Ollama timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Ollama");
            return Error.Internal($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a streaming chat completion request to Ollama's /api/chat endpoint.
    /// Ollama streams newline-delimited JSON objects (not SSE format).
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An async enumerable of stream chunks.</returns>
    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        JsonObject requestBody = BuildOllamaRequestBody(request, stream: true);
        string requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{Endpoint}/api/chat")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage httpResponse = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
            _logger.LogError("Ollama streaming request failed: {ErrorBody}", errorBody);
            yield break;
        }

        using Stream responseStream = await httpResponse.Content.ReadAsStreamAsync(ct);
        using StreamReader reader = new(responseStream);

        int index = 0;
        bool isFirst = true;

        while (!reader.EndOfStream)
        {
            string? line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonNode? chunkNode;
            try
            {
                chunkNode = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                _logger.LogWarning("Failed to parse Ollama stream chunk: {Line}", line);
                continue;
            }

            if (chunkNode is null)
            {
                continue;
            }

            string content = chunkNode["message"]?["content"]?.GetValue<string>() ?? "";
            bool done = chunkNode["done"]?.GetValue<bool>() ?? false;
            string? finishReason = done ? (chunkNode["done_reason"]?.GetValue<string>() ?? "stop") : null;

            UsageInfo? usage = null;
            if (done)
            {
                int promptTokens = chunkNode["prompt_eval_count"]?.GetValue<int>() ?? 0;
                int completionTokens = chunkNode["eval_count"]?.GetValue<int>() ?? 0;
                usage = new UsageInfo(promptTokens, completionTokens, promptTokens + completionTokens);
            }

            yield return new StreamChunk
            {
                Content = content,
                Index = index,
                LogprobsEntry = null,
                FinishReason = finishReason,
                IsFirst = isFirst,
                Usage = usage
            };

            isFirst = false;
            index++;

            if (done)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Retrieves information about the currently loaded model via /api/show.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing model information.</returns>
    public async Task<Result<ModelInfo>> GetModelInfoAsync(CancellationToken ct)
    {
        try
        {
            Result<IReadOnlyList<AvailableModel>> modelsResult = await ListAvailableModelsAsync(ct);
            if (modelsResult.IsFailure)
            {
                return Error.Unavailable(modelsResult.Error.Message);
            }

            IReadOnlyList<AvailableModel> models = modelsResult.Value;
            if (models.Count == 0)
            {
                return Error.NotFound("No models available on Ollama.");
            }

            AvailableModel firstModel = models[0];
            return new ModelInfo(
                ModelId: firstModel.ModelId,
                OwnedBy: "ollama",
                MaxContextLength: 4096,
                Capabilities: Capabilities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get model info from Ollama");
            return Error.Unavailable($"Failed to get model info: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks the health of the Ollama server by calling the root endpoint.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the health status.</returns>
    public async Task<Result<HealthStatus>> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(Endpoint, ct);

            bool isHealthy = response.IsSuccessStatusCode;
            string? model = null;

            if (isHealthy)
            {
                Result<ModelInfo> modelInfo = await GetModelInfoAsync(ct);
                if (modelInfo.IsSuccess)
                {
                    model = modelInfo.Value.ModelId;
                }
            }

            return new HealthStatus(
                IsHealthy: isHealthy,
                ProviderName: ProviderName,
                Endpoint: Endpoint,
                Model: model,
                LastCheckAt: DateTime.UtcNow,
                ErrorMessage: isHealthy ? null : "Ollama is not responding.");
        }
        catch (Exception ex)
        {
            return new HealthStatus(
                IsHealthy: false,
                ProviderName: ProviderName,
                Endpoint: Endpoint,
                Model: null,
                LastCheckAt: DateTime.UtcNow,
                ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Ollama does not expose a metrics endpoint. Returns an unavailable error.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An error result indicating metrics are not supported.</returns>
    public Task<Result<ProviderMetrics>> GetMetricsAsync(CancellationToken ct)
    {
        return Task.FromResult<Result<ProviderMetrics>>(
            Error.Unavailable("Metrics are not supported by Ollama."));
    }

    /// <summary>
    /// Ollama does not expose a tokenization endpoint. Returns an unavailable error.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An error result indicating tokenization is not supported.</returns>
    public Task<Result<TokenizeResponse>> TokenizeAsync(string text, CancellationToken ct)
    {
        return Task.FromResult<Result<TokenizeResponse>>(
            Error.Unavailable("Tokenization is not supported by Ollama."));
    }

    /// <inheritdoc />
    public Task<Result<DetokenizeResponse>> DetokenizeAsync(IReadOnlyList<int> tokenIds, CancellationToken ct)
    {
        return Task.FromResult<Result<DetokenizeResponse>>(
            Error.Unavailable("Detokenization is not supported by Ollama."));
    }

    /// <inheritdoc />
    public Task<Result<TokenizerInfo>> GetTokenizerInfoAsync(CancellationToken ct)
    {
        return Task.FromResult<Result<TokenizerInfo>>(
            Error.Unavailable("Tokenizer info is not available from Ollama."));
    }

    /// <summary>
    /// Lists all models available on the Ollama server via /api/tags.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of available models.</returns>
    public async Task<Result<IReadOnlyList<AvailableModel>>> ListAvailableModelsAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage httpResponse = await _httpClient.GetAsync(
                $"{Endpoint}/api/tags", ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                return Error.Unavailable($"Failed to list models: {errorBody}");
            }

            string responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
            JsonNode? responseNode = JsonNode.Parse(responseJson);
            JsonArray? modelsArray = responseNode?["models"]?.AsArray();

            if (modelsArray is null)
            {
                return Result.Success<IReadOnlyList<AvailableModel>>(Array.Empty<AvailableModel>());
            }

            List<AvailableModel> models = new();
            foreach (JsonNode? modelNode in modelsArray)
            {
                if (modelNode is null)
                {
                    continue;
                }

                string name = modelNode["name"]?.GetValue<string>() ?? "";
                string model = modelNode["model"]?.GetValue<string>() ?? name;
                long? size = modelNode["size"]?.GetValue<long>();
                string? quantLevel = modelNode["details"]?["quantization_level"]?.GetValue<string>();
                string? family = modelNode["details"]?["family"]?.GetValue<string>();

                models.Add(new AvailableModel(
                    ModelId: model,
                    Name: name,
                    Size: size,
                    QuantizationLevel: quantLevel,
                    Family: family));
            }

            return Result.Success<IReadOnlyList<AvailableModel>>(models);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to list models from Ollama");
            return Error.Unavailable($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads (pulls) a model on the Ollama server via /api/pull.
    /// </summary>
    /// <param name="modelId">The model identifier to load.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> LoadModelAsync(string modelId, CancellationToken ct)
    {
        try
        {
            JsonObject requestBody = new() { ["name"] = modelId };
            string requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
            using StringContent content = new(requestJson, Encoding.UTF8, "application/json");

            _logger.LogInformation("Pulling model {ModelId} on Ollama", modelId);

            using HttpResponseMessage httpResponse = await _httpClient.PostAsync(
                $"{Endpoint}/api/pull", content, ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                return Result.Failure(Error.Unavailable($"Failed to pull model: {errorBody}"));
            }

            // Read through the streaming pull response to completion
            using Stream responseStream = await httpResponse.Content.ReadAsStreamAsync(ct);
            using StreamReader reader = new(responseStream);
            while (!reader.EndOfStream)
            {
                await reader.ReadLineAsync(ct);
            }

            _logger.LogInformation("Successfully pulled model {ModelId} on Ollama", modelId);
            return Result.Success();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to pull model {ModelId} on Ollama", modelId);
            return Result.Failure(Error.Unavailable($"Connection failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Unloads a model from the Ollama server by sending a request with keep_alive set to 0.
    /// </summary>
    /// <param name="modelId">The model identifier to unload.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> UnloadModelAsync(string modelId, CancellationToken ct)
    {
        try
        {
            JsonObject requestBody = new()
            {
                ["model"] = modelId,
                ["keep_alive"] = 0
            };
            string requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
            using StringContent content = new(requestJson, Encoding.UTF8, "application/json");

            using HttpResponseMessage httpResponse = await _httpClient.PostAsync(
                $"{Endpoint}/api/generate", content, ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                return Result.Failure(Error.Unavailable($"Failed to unload model: {errorBody}"));
            }

            _logger.LogInformation("Unloaded model {ModelId} from Ollama", modelId);
            return Result.Success();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to unload model {ModelId} from Ollama", modelId);
            return Result.Failure(Error.Unavailable($"Connection failed: {ex.Message}"));
        }
    }

    private static JsonObject BuildOllamaRequestBody(ChatRequest request, bool stream)
    {
        JsonArray messagesArray = new();
        foreach (ChatMessage message in request.Messages)
        {
            JsonObject messageObj = new()
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            };
            messagesArray.Add(messageObj);
        }

        JsonObject body = new()
        {
            ["model"] = request.Model,
            ["messages"] = messagesArray,
            ["stream"] = stream
        };

        JsonObject options = new();
        bool hasOptions = false;

        if (request.Temperature.HasValue)
        {
            options["temperature"] = request.Temperature.Value;
            hasOptions = true;
        }

        if (request.TopP.HasValue)
        {
            options["top_p"] = request.TopP.Value;
            hasOptions = true;
        }

        if (request.TopK.HasValue)
        {
            options["top_k"] = request.TopK.Value;
            hasOptions = true;
        }

        if (request.MaxTokens.HasValue)
        {
            options["num_predict"] = request.MaxTokens.Value;
            hasOptions = true;
        }

        if (request.FrequencyPenalty.HasValue)
        {
            options["frequency_penalty"] = request.FrequencyPenalty.Value;
            hasOptions = true;
        }

        if (request.PresencePenalty.HasValue)
        {
            options["presence_penalty"] = request.PresencePenalty.Value;
            hasOptions = true;
        }

        if (request.StopSequences is { Count: > 0 })
        {
            JsonArray stopArray = new();
            foreach (string stop in request.StopSequences)
            {
                stopArray.Add(stop);
            }
            options["stop"] = stopArray;
            hasOptions = true;
        }

        if (hasOptions)
        {
            body["options"] = options;
        }

        return body;
    }
}

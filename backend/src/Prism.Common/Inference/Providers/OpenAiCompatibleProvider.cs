using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference.Providers;

/// <summary>
/// Base implementation for OpenAI-compatible inference providers.
/// Handles chat completions via /v1/chat/completions, model listing via /v1/models,
/// and SSE stream parsing. vLLM, LM Studio, and other OpenAI-compatible servers
/// inherit from this class.
/// </summary>
public class OpenAiCompatibleProvider : IInferenceProvider
{
    /// <summary>
    /// The HTTP client used for making API requests.
    /// </summary>
    protected readonly HttpClient HttpClient;

    /// <summary>
    /// The logger instance for this provider.
    /// </summary>
    protected readonly ILogger Logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiCompatibleProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with the provider's base URL.</param>
    /// <param name="providerName">The display name for this provider instance.</param>
    /// <param name="endpoint">The base endpoint URL.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenAiCompatibleProvider(
        HttpClient httpClient,
        string providerName,
        string endpoint,
        ILogger logger)
    {
        HttpClient = httpClient;
        ProviderName = providerName;
        Endpoint = endpoint.TrimEnd('/');
        Logger = logger;
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
    /// Gets the capabilities of this provider. Override in derived classes for provider-specific capabilities.
    /// </summary>
    public virtual ProviderCapabilities Capabilities { get; } = new()
    {
        SupportsChat = true,
        SupportsStreaming = true,
        SupportsLogprobs = true,
        MaxTopLogprobs = 5,
        SupportsTokenize = false,
        SupportsGuidedDecoding = false,
        SupportsMetrics = false,
        SupportsHotReload = false,
        SupportsSystemMessages = true,
        SupportsFrequencyPenalty = true,
        SupportsPresencePenalty = true,
        SupportsStopSequences = true
    };

    /// <summary>
    /// Sends a chat completion request to the OpenAI-compatible endpoint.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the chat response.</returns>
    public virtual async Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        try
        {
            JsonObject requestBody = BuildRequestBody(request, stream: false);

            using HttpResponseMessage httpResponse = await HttpClient.PostAsJsonAsync(
                $"{Endpoint}/v1/chat/completions",
                requestBody,
                JsonOptions,
                ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                Logger.LogError("Chat request failed with status {StatusCode}: {ErrorBody}",
                    (int)httpResponse.StatusCode, errorBody);
                return Error.Unavailable($"Provider returned {(int)httpResponse.StatusCode}: {errorBody}");
            }

            string responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
            JsonNode? responseNode = JsonNode.Parse(responseJson);

            if (responseNode is null)
            {
                return Error.Internal("Failed to parse provider response.");
            }

            return ParseChatResponse(responseNode, request.Model);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Connection failed to {ProviderName} at {Endpoint}", ProviderName, Endpoint);
            return Error.Unavailable($"Connection to {ProviderName} failed: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Logger.LogError(ex, "Request to {ProviderName} timed out", ProviderName);
            return Error.Unavailable($"Request to {ProviderName} timed out.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error calling {ProviderName}", ProviderName);
            return Error.Internal($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a streaming chat completion request and yields chunks as SSE events arrive.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An async enumerable of stream chunks.</returns>
    public virtual async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        JsonObject requestBody = BuildRequestBody(request, stream: true);
        string requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{Endpoint}/v1/chat/completions")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        using HttpResponseMessage httpResponse = await HttpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
            Logger.LogError("Streaming request failed with status {StatusCode}: {ErrorBody}",
                (int)httpResponse.StatusCode, errorBody);
            yield break;
        }

        using Stream responseStream = await httpResponse.Content.ReadAsStreamAsync(ct);
        using StreamReader reader = new(responseStream);

        int index = 0;
        bool isFirst = true;

        while (!reader.EndOfStream)
        {
            string? line = await reader.ReadLineAsync(ct);

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            string data = line["data: ".Length..];

            if (data == "[DONE]")
            {
                break;
            }

            StreamChunk? chunk = ParseStreamChunk(data, index, isFirst);
            if (chunk is not null)
            {
                yield return chunk;
                isFirst = false;
                index++;
            }
        }
    }

    /// <summary>
    /// Retrieves information about models available on the provider.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing model information for the first available model.</returns>
    public virtual async Task<Result<ModelInfo>> GetModelInfoAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage httpResponse = await HttpClient.GetAsync(
                $"{Endpoint}/v1/models", ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                return Error.Unavailable($"Failed to get model info: {errorBody}");
            }

            string responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
            JsonNode? responseNode = JsonNode.Parse(responseJson);
            JsonArray? dataArray = responseNode?["data"]?.AsArray();

            if (dataArray is null || dataArray.Count == 0)
            {
                return Error.NotFound("No models found on provider.");
            }

            JsonNode? firstModel = dataArray[0];
            string modelId = firstModel?["id"]?.GetValue<string>() ?? "unknown";
            string? ownedBy = firstModel?["owned_by"]?.GetValue<string>();

            return new ModelInfo(
                ModelId: modelId,
                OwnedBy: ownedBy,
                MaxContextLength: 4096,
                Capabilities: Capabilities);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Failed to get model info from {ProviderName}", ProviderName);
            return Error.Unavailable($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks the health of the provider by attempting to list models.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the health status.</returns>
    public virtual async Task<Result<HealthStatus>> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            Result<ModelInfo> modelResult = await GetModelInfoAsync(ct);

            return new HealthStatus(
                IsHealthy: modelResult.IsSuccess,
                ProviderName: ProviderName,
                Endpoint: Endpoint,
                Model: modelResult.IsSuccess ? modelResult.Value.ModelId : null,
                LastCheckAt: DateTime.UtcNow,
                ErrorMessage: modelResult.IsFailure ? modelResult.Error.Message : null);
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
    /// Retrieves provider metrics. Not supported by the base OpenAI-compatible provider.
    /// Override in derived classes that support metrics.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An error result indicating metrics are not supported.</returns>
    public virtual Task<Result<ProviderMetrics>> GetMetricsAsync(CancellationToken ct)
    {
        return Task.FromResult<Result<ProviderMetrics>>(
            Error.Unavailable($"Metrics are not supported by {ProviderName}."));
    }

    /// <summary>
    /// Tokenizes text. Not supported by the base OpenAI-compatible provider.
    /// Override in derived classes that support tokenization.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An error result indicating tokenization is not supported.</returns>
    public virtual Task<Result<TokenizeResponse>> TokenizeAsync(string text, CancellationToken ct)
    {
        return Task.FromResult<Result<TokenizeResponse>>(
            Error.Unavailable($"Tokenization is not supported by {ProviderName}."));
    }

    /// <summary>
    /// Detokenizes a list of token IDs back to text. Not supported by default.
    /// Override in derived classes that support detokenization.
    /// </summary>
    /// <param name="tokenIds">The token IDs to decode.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An error result indicating detokenization is not supported.</returns>
    public virtual Task<Result<DetokenizeResponse>> DetokenizeAsync(IReadOnlyList<int> tokenIds, CancellationToken ct)
    {
        return Task.FromResult<Result<DetokenizeResponse>>(
            Error.Unavailable($"Detokenization is not supported by {ProviderName}."));
    }

    /// <summary>
    /// Gets tokenizer information. Not supported by default.
    /// Override in derived classes that can provide tokenizer metadata.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An error result indicating tokenizer info is not available.</returns>
    public virtual Task<Result<TokenizerInfo>> GetTokenizerInfoAsync(CancellationToken ct)
    {
        return Task.FromResult<Result<TokenizerInfo>>(
            Error.Unavailable($"Tokenizer info is not available from {ProviderName}."));
    }

    /// <summary>
    /// Builds the JSON request body for an OpenAI-compatible chat completion request.
    /// </summary>
    /// <param name="request">The chat request to convert.</param>
    /// <param name="stream">Whether to enable streaming.</param>
    /// <returns>A JSON object representing the request body.</returns>
    protected virtual JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonArray messagesArray = new();
        foreach (ChatMessage message in request.Messages)
        {
            JsonObject messageObj = new()
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            };
            if (message.Name is not null)
            {
                messageObj["name"] = message.Name;
            }
            messagesArray.Add(messageObj);
        }

        JsonObject body = new()
        {
            ["model"] = request.Model,
            ["messages"] = messagesArray,
            ["stream"] = stream
        };

        if (request.Temperature.HasValue)
        {
            body["temperature"] = request.Temperature.Value;
        }

        if (request.TopP.HasValue)
        {
            body["top_p"] = request.TopP.Value;
        }

        if (request.MaxTokens.HasValue)
        {
            body["max_tokens"] = request.MaxTokens.Value;
        }

        if (request.FrequencyPenalty.HasValue)
        {
            body["frequency_penalty"] = request.FrequencyPenalty.Value;
        }

        if (request.PresencePenalty.HasValue)
        {
            body["presence_penalty"] = request.PresencePenalty.Value;
        }

        if (request.StopSequences is { Count: > 0 })
        {
            JsonArray stopArray = new();
            foreach (string stop in request.StopSequences)
            {
                stopArray.Add(stop);
            }
            body["stop"] = stopArray;
        }

        if (request.Logprobs)
        {
            body["logprobs"] = true;
            if (request.TopLogprobs.HasValue)
            {
                body["top_logprobs"] = request.TopLogprobs.Value;
            }
        }

        if (request.ResponseFormat is not null)
        {
            body["response_format"] = JsonNode.Parse(request.ResponseFormat) ?? new JsonObject { ["type"] = request.ResponseFormat };
        }

        if (stream)
        {
            body["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        return body;
    }

    /// <summary>
    /// Parses a non-streaming chat response from the provider's JSON response.
    /// </summary>
    /// <param name="responseNode">The JSON response node.</param>
    /// <param name="requestModel">The model identifier from the request.</param>
    /// <returns>A result containing the parsed chat response.</returns>
    protected virtual Result<ChatResponse> ParseChatResponse(JsonNode responseNode, string requestModel)
    {
        JsonNode? choice = responseNode["choices"]?[0];
        if (choice is null)
        {
            return Error.Internal("No choices in provider response.");
        }

        string content = choice["message"]?["content"]?.GetValue<string>() ?? "";
        string? finishReason = choice["finish_reason"]?.GetValue<string>();

        JsonNode? usageNode = responseNode["usage"];
        UsageInfo? usage = usageNode is not null
            ? new UsageInfo(
                PromptTokens: usageNode["prompt_tokens"]?.GetValue<int>() ?? 0,
                CompletionTokens: usageNode["completion_tokens"]?.GetValue<int>() ?? 0,
                TotalTokens: usageNode["total_tokens"]?.GetValue<int>() ?? 0)
            : null;

        LogprobsData? logprobsData = ParseLogprobs(choice["logprobs"]);
        string modelId = responseNode["model"]?.GetValue<string>() ?? requestModel;

        return new ChatResponse
        {
            Content = content,
            FinishReason = finishReason,
            Usage = usage,
            LogprobsData = logprobsData,
            ModelId = modelId,
            Timing = null
        };
    }

    /// <summary>
    /// Parses a single SSE data chunk from a streaming response.
    /// </summary>
    /// <param name="data">The JSON string from the SSE data field.</param>
    /// <param name="index">The zero-based index of this chunk in the stream.</param>
    /// <param name="isFirst">Whether this is the first chunk in the stream.</param>
    /// <returns>A parsed stream chunk, or null if the data could not be parsed.</returns>
    protected virtual StreamChunk? ParseStreamChunk(string data, int index, bool isFirst)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(data);
            if (node is null)
            {
                return null;
            }

            JsonNode? choicesNode = node["choices"];
            JsonNode? choice = choicesNode is JsonArray choicesArray && choicesArray.Count > 0
                ? choicesArray[0]
                : null;
            JsonNode? delta = choice?["delta"];
            string content = delta?["content"]?.GetValue<string>() ?? "";
            string? finishReason = choice?["finish_reason"]?.GetValue<string>();

            TokenLogprob? logprobEntry = null;
            JsonNode? logprobsNode = choice?["logprobs"];
            if (logprobsNode is not null)
            {
                JsonNode? contentLogprobs = logprobsNode["content"];
                if (contentLogprobs is JsonArray contentArray && contentArray.Count > 0)
                {
                    JsonNode? tokenNode = contentArray[0];
                    logprobEntry = ParseSingleTokenLogprob(tokenNode);
                }
            }

            JsonNode? usageNode = node["usage"];
            UsageInfo? usage = usageNode is not null
                ? new UsageInfo(
                    PromptTokens: usageNode["prompt_tokens"]?.GetValue<int>() ?? 0,
                    CompletionTokens: usageNode["completion_tokens"]?.GetValue<int>() ?? 0,
                    TotalTokens: usageNode["total_tokens"]?.GetValue<int>() ?? 0)
                : null;

            return new StreamChunk
            {
                Content = content,
                Index = index,
                LogprobsEntry = logprobEntry,
                FinishReason = finishReason,
                IsFirst = isFirst,
                Usage = usage
            };
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse stream chunk: {Data}", data);
            return null;
        }
    }

    /// <summary>
    /// Parses the logprobs object from a non-streaming response choice.
    /// </summary>
    /// <param name="logprobsNode">The logprobs JSON node from a choice.</param>
    /// <returns>Parsed log probability data, or null if not present.</returns>
    protected LogprobsData? ParseLogprobs(JsonNode? logprobsNode)
    {
        if (logprobsNode is null)
        {
            return null;
        }

        JsonArray? contentArray = logprobsNode["content"]?.AsArray();
        if (contentArray is null || contentArray.Count == 0)
        {
            return null;
        }

        List<TokenLogprob> tokens = new();
        foreach (JsonNode? tokenNode in contentArray)
        {
            TokenLogprob? parsed = ParseSingleTokenLogprob(tokenNode);
            if (parsed is not null)
            {
                tokens.Add(parsed);
            }
        }

        return new LogprobsData { Tokens = tokens };
    }

    private static TokenLogprob? ParseSingleTokenLogprob(JsonNode? tokenNode)
    {
        if (tokenNode is null)
        {
            return null;
        }

        string token = tokenNode["token"]?.GetValue<string>() ?? "";
        double logprob = tokenNode["logprob"]?.GetValue<double>() ?? 0;

        List<TopLogprob> topLogprobs = new();
        JsonArray? topArray = tokenNode["top_logprobs"]?.AsArray();
        if (topArray is not null)
        {
            foreach (JsonNode? topNode in topArray)
            {
                if (topNode is not null)
                {
                    topLogprobs.Add(new TopLogprob
                    {
                        Token = topNode["token"]?.GetValue<string>() ?? "",
                        Logprob = topNode["logprob"]?.GetValue<double>() ?? 0
                    });
                }
            }
        }

        return new TokenLogprob
        {
            Token = token,
            Logprob = logprob,
            TopLogprobs = topLogprobs,
            ByteOffset = tokenNode["bytes"]?.AsArray()?.Count
        };
    }
}

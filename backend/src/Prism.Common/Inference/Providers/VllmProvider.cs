using System.Globalization;
using System.Text.Json.Nodes;
using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference.Providers;

/// <summary>
/// vLLM-specific inference provider that extends <see cref="OpenAiCompatibleProvider"/>
/// with support for vLLM's extended capabilities: Prometheus metrics endpoint,
/// tokenization endpoint, guided decoding, and higher logprob limits.
/// </summary>
public sealed class VllmProvider : OpenAiCompatibleProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VllmProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with the vLLM server's base URL.</param>
    /// <param name="providerName">The display name for this provider instance.</param>
    /// <param name="endpoint">The base endpoint URL of the vLLM server.</param>
    /// <param name="logger">The logger instance.</param>
    public VllmProvider(
        HttpClient httpClient,
        string providerName,
        string endpoint,
        ILogger<VllmProvider> logger)
        : base(httpClient, providerName, endpoint, logger)
    {
    }

    /// <summary>
    /// Gets the vLLM-specific capabilities including logprobs (up to 20), metrics, tokenization, and guided decoding.
    /// </summary>
    public override ProviderCapabilities Capabilities { get; } = new()
    {
        SupportsChat = true,
        SupportsStreaming = true,
        SupportsLogprobs = true,
        MaxTopLogprobs = 20,
        SupportsTokenize = true,
        SupportsGuidedDecoding = true,
        SupportsMetrics = true,
        SupportsHotReload = false,
        SupportsSystemMessages = true,
        SupportsFrequencyPenalty = true,
        SupportsPresencePenalty = true,
        SupportsStopSequences = true
    };

    /// <summary>
    /// Retrieves runtime performance metrics from vLLM's Prometheus /metrics endpoint.
    /// Parses Prometheus text format to extract GPU utilization, KV cache usage, queue depth, etc.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing parsed provider metrics.</returns>
    public override async Task<Result<ProviderMetrics>> GetMetricsAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage httpResponse = await HttpClient.GetAsync(
                $"{Endpoint}/metrics", ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                return Error.Unavailable($"Failed to get metrics: {errorBody}");
            }

            string metricsText = await httpResponse.Content.ReadAsStringAsync(ct);
            ProviderMetrics metrics = ParsePrometheusMetrics(metricsText);
            return metrics;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Failed to get metrics from {ProviderName}", ProviderName);
            return Error.Unavailable($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tokenizes text using vLLM's /tokenize endpoint.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the tokenization response.</returns>
    public override async Task<Result<TokenizeResponse>> TokenizeAsync(string text, CancellationToken ct)
    {
        try
        {
            // Get the current model ID for the tokenize request
            Result<ModelInfo> modelResult = await GetModelInfoAsync(ct);
            string modelId = modelResult.IsSuccess ? modelResult.Value.ModelId : "";

            JsonObject requestBody = new()
            {
                ["model"] = modelId,
                ["prompt"] = text
            };

            string requestJson = JsonSerializer.Serialize(requestBody);
            using StringContent content = new(requestJson, System.Text.Encoding.UTF8, "application/json");

            using HttpResponseMessage httpResponse = await HttpClient.PostAsync(
                $"{Endpoint}/tokenize", content, ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                return Error.Unavailable($"Tokenization failed: {errorBody}");
            }

            string responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
            JsonNode? responseNode = JsonNode.Parse(responseJson);

            if (responseNode is null)
            {
                return Error.Internal("Failed to parse tokenization response.");
            }

            JsonArray? tokensArray = responseNode["tokens"]?.AsArray();
            int tokenCount = responseNode["count"]?.GetValue<int>() ?? tokensArray?.Count ?? 0;

            List<TokenInfo> tokens = new();
            if (tokensArray is not null)
            {
                foreach (JsonNode? tokenNode in tokensArray)
                {
                    if (tokenNode is null)
                    {
                        continue;
                    }

                    // vLLM tokenize returns an array of token IDs
                    if (tokenNode.GetValueKind() == System.Text.Json.JsonValueKind.Number)
                    {
                        int tokenId = tokenNode.GetValue<int>();
                        tokens.Add(new TokenInfo(
                            Id: tokenId,
                            Text: $"<token_{tokenId}>",
                            ByteLength: 0));
                    }
                    else if (tokenNode.GetValueKind() == System.Text.Json.JsonValueKind.Object)
                    {
                        int tokenId = tokenNode["id"]?.GetValue<int>() ?? 0;
                        string tokenText = tokenNode["text"]?.GetValue<string>() ?? "";
                        tokens.Add(new TokenInfo(
                            Id: tokenId,
                            Text: tokenText,
                            ByteLength: System.Text.Encoding.UTF8.GetByteCount(tokenText)));
                    }
                }
            }

            return new TokenizeResponse(tokens, tokenCount > 0 ? tokenCount : tokens.Count);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Tokenization failed for {ProviderName}", ProviderName);
            return Error.Unavailable($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves tokenizer information from vLLM by probing the model info and tokenization endpoints.
    /// Detects special tokens (BOS, EOS) by tokenizing an empty prompt.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing tokenizer metadata.</returns>
    public override async Task<Result<TokenizerInfo>> GetTokenizerInfoAsync(CancellationToken ct)
    {
        try
        {
            Result<ModelInfo> modelResult = await GetModelInfoAsync(ct);
            string modelId = modelResult.IsSuccess ? modelResult.Value.ModelId : "";

            var specialTokens = new Dictionary<string, string>();

            // Probe for BOS/EOS by tokenizing an empty string — many tokenizers prepend BOS
            Result<TokenizeResponse> emptyResult = await TokenizeAsync("", ct);
            if (emptyResult.IsSuccess && emptyResult.Value.Tokens.Count > 0)
            {
                TokenInfo firstToken = emptyResult.Value.Tokens[0];
                specialTokens["BOS"] = firstToken.Text;
            }

            // Probe for common special tokens by attempting detokenize on well-known IDs
            // Most tokenizers use ID 0-2 for special tokens
            int?[] probeIds = [0, 1, 2];
            string[] probeNames = ["PAD", "UNK/BOS", "EOS"];

            for (int i = 0; i < probeIds.Length; i++)
            {
                Result<DetokenizeResponse> probeResult = await DetokenizeAsync([probeIds[i]!.Value], ct);
                if (probeResult.IsSuccess && !string.IsNullOrEmpty(probeResult.Value.Text))
                {
                    string text = probeResult.Value.Text.Trim();
                    if (text.StartsWith('<') && text.EndsWith('>'))
                    {
                        specialTokens[probeNames[i]] = text;
                    }
                }
            }

            return new TokenizerInfo(
                VocabSize: null, // vLLM doesn't directly expose vocab size
                TokenizerType: null,
                SpecialTokens: specialTokens,
                ModelId: modelId);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "GetTokenizerInfo failed for {ProviderName}", ProviderName);
            return Error.Unavailable($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Detokenizes a list of token IDs back to text using vLLM's <c>/detokenize</c> endpoint.
    /// </summary>
    /// <param name="tokenIds">The token IDs to decode.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the decoded text on success.</returns>
    public override async Task<Result<DetokenizeResponse>> DetokenizeAsync(IReadOnlyList<int> tokenIds, CancellationToken ct)
    {
        try
        {
            Result<ModelInfo> modelResult = await GetModelInfoAsync(ct);
            string modelId = modelResult.IsSuccess ? modelResult.Value.ModelId : "";

            JsonArray idsArray = new();
            foreach (int id in tokenIds)
            {
                idsArray.Add(id);
            }

            JsonObject requestBody = new()
            {
                ["model"] = modelId,
                ["tokens"] = idsArray
            };

            string requestJson = JsonSerializer.Serialize(requestBody);
            using StringContent content = new(requestJson, System.Text.Encoding.UTF8, "application/json");

            using HttpResponseMessage httpResponse = await HttpClient.PostAsync(
                $"{Endpoint}/detokenize", content, ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
                return Error.Unavailable($"Detokenization failed: {errorBody}");
            }

            string responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
            JsonNode? responseNode = JsonNode.Parse(responseJson);

            string decodedText = responseNode?["prompt"]?.GetValue<string>() ?? "";

            return new DetokenizeResponse(decodedText);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Detokenization failed for {ProviderName}", ProviderName);
            return Error.Unavailable($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the request body with vLLM-specific extensions like guided decoding parameters.
    /// </summary>
    /// <param name="request">The chat request.</param>
    /// <param name="stream">Whether to enable streaming.</param>
    /// <returns>The JSON request body with vLLM extensions.</returns>
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        if (request.TopK.HasValue)
        {
            body["top_k"] = request.TopK.Value;
        }

        if (!request.EnableThinking)
        {
            body["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false };
        }

        // When the last message is from the assistant (prefill/continuation pattern),
        // tell vLLM not to close the turn with <|im_end|> so the model continues
        // generating from the prefix rather than starting a new turn.
        if (request.Messages.Count > 0
            && request.Messages[^1].Role == ChatMessage.AssistantRole)
        {
            body["continue_final_message"] = true;
            body["add_generation_prompt"] = false;
        }

        return body;
    }

    private static ProviderMetrics ParsePrometheusMetrics(string metricsText)
    {
        double? requestsPerSecond = null;
        int? queueDepth = null;
        double? gpuUtilization = null;
        long? gpuMemoryUsed = null;
        long? gpuMemoryTotal = null;
        double? kvCacheUtilization = null;
        int? activeRequests = null;

        string[] lines = metricsText.Split('\n');
        foreach (string line in lines)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("vllm:num_requests_running", StringComparison.Ordinal))
            {
                activeRequests = ParseMetricIntValue(line);
            }
            else if (line.StartsWith("vllm:num_requests_waiting", StringComparison.Ordinal))
            {
                queueDepth = ParseMetricIntValue(line);
            }
            else if (line.StartsWith("vllm:gpu_cache_usage_perc", StringComparison.Ordinal))
            {
                kvCacheUtilization = ParseMetricDoubleValue(line);
                if (kvCacheUtilization.HasValue)
                {
                    kvCacheUtilization *= 100;
                }
            }
            else if (line.StartsWith("vllm:avg_generation_throughput_toks_per_s", StringComparison.Ordinal))
            {
                requestsPerSecond = ParseMetricDoubleValue(line);
            }
        }

        return new ProviderMetrics(
            RequestsPerSecond: requestsPerSecond,
            QueueDepth: queueDepth,
            GpuUtilization: gpuUtilization,
            GpuMemoryUsed: gpuMemoryUsed,
            GpuMemoryTotal: gpuMemoryTotal,
            KvCacheUtilization: kvCacheUtilization,
            ActiveRequests: activeRequests);
    }

    private static int? ParseMetricIntValue(string line)
    {
        string[] parts = line.Split(' ');
        if (parts.Length >= 2 && double.TryParse(parts[^1], NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
        {
            return (int)value;
        }

        return null;
    }

    private static double? ParseMetricDoubleValue(string line)
    {
        string[] parts = line.Split(' ');
        if (parts.Length >= 2 && double.TryParse(parts[^1], NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
        {
            return value;
        }

        return null;
    }
}

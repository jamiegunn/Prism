using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Prism.Common.Inference;
using Prism.Common.Inference.Providers;

namespace Prism.Features.Models.Application;

/// <summary>
/// Factory for creating <see cref="IInferenceProvider"/> instances from connection details.
/// Used by Models feature handlers to communicate with registered inference provider instances.
/// </summary>
public sealed class InferenceProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly InferenceClientOptions _options;
    private readonly Channel<InferenceRecordData>? _recordChannel;

    /// <summary>
    /// Initializes a new instance of the <see cref="InferenceProviderFactory"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating provider HTTP clients.</param>
    /// <param name="loggerFactory">The logger factory for creating provider-specific loggers.</param>
    /// <param name="options">HTTP timeout configuration. Defaults are used when omitted.</param>
    /// <param name="recordChannel">
    /// Channel receiving a record of every inference call. When supplied, every provider this
    /// factory creates is wrapped so that no feature can perform inference without it being
    /// recorded. When omitted, recording is disabled - intended for unit tests only.
    /// </param>
    public InferenceProviderFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<InferenceClientOptions>? options = null,
        Channel<InferenceRecordData>? recordChannel = null)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _options = options?.Value ?? new InferenceClientOptions();
        _recordChannel = recordChannel;
    }

    /// <summary>
    /// Creates an <see cref="IInferenceProvider"/> configured for the given endpoint and provider type.
    /// </summary>
    /// <param name="name">The display name for this provider instance.</param>
    /// <param name="endpoint">The base endpoint URL of the inference provider.</param>
    /// <param name="providerType">The type of inference provider to create.</param>
    /// <returns>A configured <see cref="IInferenceProvider"/> instance.</returns>
    public IInferenceProvider CreateProvider(string name, string endpoint, InferenceProviderType providerType)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
        // The client covers generation, which on local models routinely runs for minutes.
        // Short deadlines belong on individual probe calls, not on the shared client.
        httpClient.Timeout = _options.Request;

        IInferenceProvider provider = providerType switch
        {
            InferenceProviderType.Vllm => new VllmProvider(
                httpClient, name, endpoint,
                _loggerFactory.CreateLogger<VllmProvider>()),
            InferenceProviderType.Ollama => new OllamaProvider(
                httpClient, name, endpoint,
                _loggerFactory.CreateLogger<OllamaProvider>()),
            InferenceProviderType.LmStudio => new OpenAiCompatibleProvider(
                httpClient, name, endpoint,
                _loggerFactory.CreateLogger<OpenAiCompatibleProvider>()),
            _ => new OpenAiCompatibleProvider(
                httpClient, name, endpoint,
                _loggerFactory.CreateLogger<OpenAiCompatibleProvider>()),
        };

        // Recording is applied here rather than at each call site. Seven features previously
        // reached providers directly, so "every call is recorded" was false and both History
        // and Analytics read from tables nothing wrote to. Wrapping at the only place providers
        // are constructed makes bypassing it impossible.
        if (_recordChannel is not null)
        {
            provider = new RecordingInferenceProvider(
                provider,
                _recordChannel,
                providerType,
                _loggerFactory.CreateLogger<RecordingInferenceProvider>());
        }

        return provider;
    }
}

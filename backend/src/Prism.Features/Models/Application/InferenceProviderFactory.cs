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

    /// <summary>
    /// Initializes a new instance of the <see cref="InferenceProviderFactory"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating provider HTTP clients.</param>
    /// <param name="loggerFactory">The logger factory for creating provider-specific loggers.</param>
    /// <param name="options">HTTP timeout configuration. Defaults are used when omitted.</param>
    public InferenceProviderFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<InferenceClientOptions>? options = null)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _options = options?.Value ?? new InferenceClientOptions();
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

        return providerType switch
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
    }
}

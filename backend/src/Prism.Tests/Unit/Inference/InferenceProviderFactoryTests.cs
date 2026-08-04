using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference;
using Prism.Features.Models.Application;

namespace Prism.Tests.Unit.Inference;

/// <summary>
/// Covers HTTP client configuration for created inference providers.
/// </summary>
/// <remarks>
/// Written to fail on the pre-fix code, which set a flat 10-second timeout on every
/// provider. Local model generation routinely exceeds that, so chat completions,
/// parameter sweeps, agent steps and Ollama model pulls were all being cancelled
/// mid-flight and surfaced as connection errors.
/// </remarks>
public sealed class InferenceProviderFactoryTests
{
    // A generation on a laptop-class model can easily run past a minute. Anything at or
    // below this would cancel legitimate work.
    private static readonly TimeSpan MinimumUsableTimeout = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData(InferenceProviderType.Vllm)]
    [InlineData(InferenceProviderType.Ollama)]
    [InlineData(InferenceProviderType.LmStudio)]
    [InlineData(InferenceProviderType.OpenAiCompatible)]
    public void CreateProvider_Allows_Time_For_A_Real_Generation(InferenceProviderType providerType)
    {
        var recording = new RecordingHttpClientFactory();
        var factory = new InferenceProviderFactory(recording, NullLoggerFactory.Instance);

        factory.CreateProvider("test", "http://localhost:8000", providerType);

        HttpClient client = Assert.Single(recording.Created);
        Assert.True(
            client.Timeout >= MinimumUsableTimeout,
            $"HttpClient timeout is {client.Timeout.TotalSeconds:0}s. Generations, sweeps and " +
            $"agent steps routinely exceed that, so this cancels real work.");
    }

    [Fact]
    public void CreateProvider_Sets_BaseAddress_With_Trailing_Slash()
    {
        var recording = new RecordingHttpClientFactory();
        var factory = new InferenceProviderFactory(recording, NullLoggerFactory.Instance);

        factory.CreateProvider("test", "http://localhost:8000/v1", InferenceProviderType.Vllm);

        HttpClient client = Assert.Single(recording.Created);
        Assert.Equal("http://localhost:8000/v1/", client.BaseAddress?.ToString());
    }

    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        public List<HttpClient> Created { get; } = [];

        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient(new NoopHandler());
            Created.Add(client);
            return client;
        }

        private sealed class NoopHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}

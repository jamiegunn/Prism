using Prism.Common.Inference.Providers;

namespace Prism.Tests.Unit.Models;

/// <summary>
/// Proofs that a pasted OpenAI-style base URL works verbatim. vLLM's docs quote the server
/// as <c>http://host:8000/v1</c>, the provider appends <c>/v1/...</c> itself, and the two
/// conventions colliding produced <c>/v1/v1/chat/completions</c> — a 404 on every call from
/// an endpoint Prism's own seeder wrote.
/// </summary>
public sealed class EndpointNormalizationTests
{
    /// <summary>
    /// A trailing /v1 (any casing, any trailing slashes) is stripped; everything else is kept
    /// verbatim minus trailing slashes. A /v1 elsewhere in the path is not touched.
    /// </summary>
    /// <param name="configured">The endpoint as the user configured it.</param>
    /// <param name="expected">The normalized server root.</param>
    [Theory]
    [InlineData("http://localhost:8000/v1", "http://localhost:8000")]
    [InlineData("http://localhost:8000/v1/", "http://localhost:8000")]
    [InlineData("http://localhost:8000/V1", "http://localhost:8000")]
    [InlineData("http://localhost:8000", "http://localhost:8000")]
    [InlineData("http://localhost:8000/", "http://localhost:8000")]
    [InlineData("https://gateway.example.com/vllm/v1", "https://gateway.example.com/vllm")]
    [InlineData("http://host/v1/proxy", "http://host/v1/proxy")]
    public void Pasted_Base_Urls_Resolve_To_The_Server_Root(string configured, string expected)
    {
        Assert.Equal(expected, OpenAiCompatibleProvider.NormalizeEndpoint(configured));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.Models.Domain;
using Prism.Features.Rag.Infrastructure;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers which endpoint embeddings are sent to.
/// </summary>
/// <remarks>
/// The provider read <c>Embedding:BaseUrl</c> or <c>Inference:DefaultEndpoint</c>, neither of
/// which exists in <c>appsettings.json</c>, and so silently fell back to a hardcoded vLLM
/// address for every deployment. An Ollama-only user got no embeddings and no explanation of
/// why RAG did not work.
/// </remarks>
[Collection("Database")]
public sealed class EmbeddingEndpointTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingEndpointTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public EmbeddingEndpointTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// With nothing configured, embeddings go to the registered instance rather than to a
    /// hardcoded address that may not be running anything.
    /// </summary>
    [Fact]
    public async Task Embeddings_Go_To_The_Registered_Instance_When_Unconfigured()
    {
        await using AppDbContext db = _fixture.CreateContext();

        string endpoint = $"http://ollama-{Guid.NewGuid():N}:11434";
        db.Set<InferenceInstance>().Add(new InferenceInstance
        {
            Name = "local ollama",
            Endpoint = endpoint,
            ProviderType = InferenceProviderType.Ollama,
        });
        await db.SaveChangesAsync();

        FakeHttpTransport transport = FakeHttpTransport.Json("""{"data":[{"embedding":[0.1,0.2]}]}""");

        var provider = new OpenAiEmbeddingProvider(
            transport,
            new ConfigurationBuilder().Build(),
            NullLogger<OpenAiEmbeddingProvider>.Instance,
            db);

        Result<float[]> result = await provider.EmbedAsync("hello", "any", CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsSuccess ? "" : result.Error.Message);

        // Asserted against every registered endpoint rather than the one just added: the shared
        // database carries instances from other tests, and "oldest instance" is not necessarily
        // this one. The property that matters is that the call went to something real from the
        // database instead of the hardcoded default.
        List<string> registered = await db.Set<InferenceInstance>()
            .AsNoTracking().Select(i => i.Endpoint).ToListAsync();

        string called = transport.Requests[^1].RequestUri!.ToString();

        Assert.DoesNotContain("localhost:8000", called, StringComparison.Ordinal);
        Assert.Contains(registered, e => called.Contains(e, StringComparison.Ordinal));
    }

    /// <summary>
    /// Explicit configuration still wins over discovery.
    /// </summary>
    [Fact]
    public async Task Configured_Endpoint_Takes_Precedence()
    {
        await using AppDbContext db = _fixture.CreateContext();

        const string configured = "http://configured-embeddings:9000";

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Embedding:BaseUrl"] = configured })
            .Build();

        FakeHttpTransport transport = FakeHttpTransport.Json("""{"data":[{"embedding":[0.1]}]}""");

        var provider = new OpenAiEmbeddingProvider(
            transport, config, NullLogger<OpenAiEmbeddingProvider>.Instance, db);

        await provider.EmbedAsync("hello", "any", CancellationToken.None);

        Assert.Contains(
            configured,
            transport.Requests[^1].RequestUri!.ToString(),
            StringComparison.Ordinal);
    }
}

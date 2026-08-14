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

    /// <summary>
    /// An endpoint that already ends in <c>/v1</c> is not given a second one.
    /// </summary>
    /// <remarks>
    /// vLLM and LM Studio are registered with the <c>/v1</c> in their endpoint — that is the
    /// address they publish. Appending <c>/v1/embeddings</c> to it produced
    /// <c>/v1/v1/embeddings</c>, a 404 on every request, so embeddings could never work against
    /// either of them however healthy they were. Ollama has no <c>/v1</c> in its endpoint, which
    /// is why the fault stayed hidden on the setup most people run.
    /// </remarks>
    [Theory]
    [InlineData("http://vllm:8000", "http://vllm:8000/v1/embeddings")]
    [InlineData("http://vllm:8000/", "http://vllm:8000/v1/embeddings")]
    [InlineData("http://vllm:8000/v1", "http://vllm:8000/v1/embeddings")]
    [InlineData("http://vllm:8000/v1/", "http://vllm:8000/v1/embeddings")]
    public async Task The_Embedding_Url_Carries_Exactly_One_V1(string endpoint, string expected)
    {
        await using AppDbContext db = _fixture.CreateContext();
        await db.Set<InferenceInstance>().ExecuteDeleteAsync();

        db.Set<InferenceInstance>().Add(new InferenceInstance
        {
            Name = "openai-compatible",
            Endpoint = endpoint,
            ProviderType = InferenceProviderType.OpenAiCompatible,
            Status = InstanceStatus.Online,
        });

        await db.SaveChangesAsync();

        FakeHttpTransport transport = FakeHttpTransport.Json("""{"data":[{"embedding":[0.1]}]}""");

        var provider = new OpenAiEmbeddingProvider(
            transport,
            new ConfigurationBuilder().Build(),
            NullLogger<OpenAiEmbeddingProvider>.Instance,
            db);

        await provider.EmbedAsync("hello", "any", CancellationToken.None);

        Assert.Equal(expected, transport.Requests[^1].RequestUri!.ToString());
    }

    /// <summary>
    /// An offline instance is not chosen while a reachable one is registered.
    /// </summary>
    /// <remarks>
    /// Found on a fresh install. The seeder registers a vLLM and an Ollama with the same
    /// timestamp and neither marked default, so "default first, then oldest" fell through to an
    /// arbitrary pick — and picked the vLLM, which nothing on the machine was running. The
    /// sample collection came up unembedded with <c>Connection refused
    /// (host.docker.internal:8000)</c> while a healthy Ollama sat one row away. Whether a server
    /// answers is the first thing that matters about it.
    /// </remarks>
    [Fact]
    public async Task An_Offline_Instance_Is_Not_Preferred_Over_A_Reachable_One()
    {
        await using AppDbContext db = _fixture.CreateContext();

        // Instances from earlier tests would make "it did not call the dead one" true by
        // accident, so the field is cleared first. Safe because the Database collection runs its
        // tests one at a time.
        await db.Set<InferenceInstance>().ExecuteDeleteAsync();

        // The dead one is made the older of the two, so "oldest instance wins" points straight at
        // it. On the real fresh install the two shared a timestamp and the tie broke this way by
        // chance; pinning it makes the test decide the behaviour rather than the row order.
        string offline = $"http://dead-vllm-{Guid.NewGuid():N}:8000/v1";
        string online = $"http://live-ollama-{Guid.NewGuid():N}:11434";

        db.Set<InferenceInstance>().AddRange(
            new InferenceInstance
            {
                Name = "seeded vllm",
                Endpoint = offline,
                ProviderType = InferenceProviderType.OpenAiCompatible,
                Status = InstanceStatus.Offline,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
            },
            new InferenceInstance
            {
                Name = "seeded ollama",
                Endpoint = online,
                ProviderType = InferenceProviderType.Ollama,
                Status = InstanceStatus.Online,
                CreatedAt = DateTime.UtcNow,
            });

        await db.SaveChangesAsync();

        FakeHttpTransport transport = FakeHttpTransport.Json("""{"data":[{"embedding":[0.1]}]}""");

        var provider = new OpenAiEmbeddingProvider(
            transport,
            new ConfigurationBuilder().Build(),
            NullLogger<OpenAiEmbeddingProvider>.Instance,
            db);

        await provider.EmbedAsync("hello", "any", CancellationToken.None);

        string called = transport.Requests[^1].RequestUri!.ToString();

        Assert.DoesNotContain(offline, called, StringComparison.Ordinal);
    }

    /// <summary>
    /// Among reachable instances the default is still the one that wins.
    /// </summary>
    [Fact]
    public async Task A_Reachable_Default_Beats_An_Older_Reachable_Instance()
    {
        await using AppDbContext db = _fixture.CreateContext();

        await db.Set<InferenceInstance>().ExecuteDeleteAsync();

        string older = $"http://older-{Guid.NewGuid():N}:11434";
        string preferred = $"http://preferred-{Guid.NewGuid():N}:11434";

        db.Set<InferenceInstance>().AddRange(
            new InferenceInstance
            {
                Name = "older online",
                Endpoint = older,
                ProviderType = InferenceProviderType.Ollama,
                Status = InstanceStatus.Online,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
            },
            new InferenceInstance
            {
                Name = "the chosen one",
                Endpoint = preferred,
                ProviderType = InferenceProviderType.Ollama,
                Status = InstanceStatus.Online,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
            });

        await db.SaveChangesAsync();

        FakeHttpTransport transport = FakeHttpTransport.Json("""{"data":[{"embedding":[0.1]}]}""");

        var provider = new OpenAiEmbeddingProvider(
            transport,
            new ConfigurationBuilder().Build(),
            NullLogger<OpenAiEmbeddingProvider>.Instance,
            db);

        await provider.EmbedAsync("hello", "any", CancellationToken.None);

        Assert.Contains(
            preferred, transport.Requests[^1].RequestUri!.ToString(), StringComparison.Ordinal);
    }
}

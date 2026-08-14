using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference.Models;
using Prism.Common.Inference.Providers;
using Prism.Common.Results;
using Prism.Tests.Support;

namespace Prism.Tests.Unit.Inference;

/// <summary>
/// Covers which model Prism reports as an Ollama server's model.
/// </summary>
/// <remarks>
/// <para>
/// The answer used to be <c>models[0]</c> from <c>/api/tags</c>, and Ollama returns that list
/// newest-pulled first. So pulling anything at all changed what every registered instance
/// claimed to be running — including pulling an embedding model, which cannot hold a
/// conversation. Doing exactly that turned every generative screen in Prism into
/// <c>"nomic-embed-text:latest" does not support chat</c>: Playground, Prompt Lab, Agents and
/// the RAG answer step, all at once, from one unrelated download.
/// </para>
/// <para>
/// Ollama will say what a model is for. <c>/api/show</c> returns a <c>capabilities</c> array —
/// <c>["embedding"]</c> against <c>["completion","tools"]</c> — so this is a question with an
/// authoritative answer rather than one to guess at from the name.
/// </para>
/// </remarks>
public sealed class OllamaModelSelectionTests
{
    /// <summary>Two models, the embedding one listed first, as a fresh pull would leave it.</summary>
    private const string TagsEmbeddingFirst = """
        {
          "models": [
            { "name": "nomic-embed-text:latest", "model": "nomic-embed-text:latest",
              "size": 274302450, "details": { "family": "nomic-bert", "quantization_level": "F16" } },
            { "name": "mistral:7b-instruct", "model": "mistral:7b-instruct",
              "size": 4372824384, "details": { "family": "llama", "quantization_level": "Q4_0" } }
          ]
        }
        """;

    /// <summary>
    /// A server whose largest model is the embedding one, so "did it filter?" is observable.
    /// </summary>
    /// <remarks>
    /// With the embedding model also the smallest, a run that never filters and a run that
    /// filters correctly both land on the chat model, and the degradation tests below would pass
    /// without exercising anything.
    /// </remarks>
    private const string TagsWithALargeEmbeddingModel = """
        {
          "models": [
            { "name": "big-embed:latest", "model": "big-embed:latest",
              "size": 9000000000, "details": { "family": "nomic-bert" } },
            { "name": "mistral:7b-instruct", "model": "mistral:7b-instruct",
              "size": 4372824384, "details": { "family": "llama" } }
          ]
        }
        """;

    private const string ShowEmbedding = """{ "capabilities": ["embedding"] }""";
    private const string ShowCompletion = """{ "capabilities": ["completion", "tools"] }""";

    /// <summary>
    /// A model that cannot chat is not the server's model, however recently it was pulled.
    /// </summary>
    [Fact]
    public async Task An_Embedding_Model_Is_Skipped_For_One_That_Can_Chat()
    {
        OllamaProvider provider = Build(FakeHttpTransport.JsonByPath(
            ("/api/tags", TagsEmbeddingFirst),
            ("nomic-embed-text", ShowEmbedding),
            ("/api/show", ShowCompletion)));

        Result<ModelInfo> info = await provider.GetModelInfoAsync(default);

        Assert.True(info.IsSuccess);
        Assert.Equal("mistral:7b-instruct", info.Value.ModelId);
    }

    /// <summary>
    /// Among models nobody chose, the largest is the one to run.
    /// </summary>
    /// <remarks>
    /// Some tiebreak is needed and download order is not one — it means "whatever was fetched
    /// last", which is how an instance named for a 7B model ended up answering from a 0.5B one
    /// and refusing prompts the larger model handles. Size is the only signal a server offers
    /// about intent: a 4.4GB download is a deliberate act in a way that a 400MB one is not. This
    /// decides nothing when a model has been chosen, which is the normal case.
    /// </remarks>
    [Fact]
    public async Task The_Largest_Chat_Model_Is_Preferred_Over_A_Smaller_One()
    {
        const string threeModels = """
            {
              "models": [
                { "name": "nomic-embed-text:latest", "model": "nomic-embed-text:latest",
                  "size": 274302450, "details": { "family": "nomic-bert" } },
                { "name": "qwen2.5:0.5b", "model": "qwen2.5:0.5b",
                  "size": 397821319, "details": { "family": "qwen2" } },
                { "name": "mistral:7b-instruct", "model": "mistral:7b-instruct",
                  "size": 4372824384, "details": { "family": "llama" } }
              ]
            }
            """;

        OllamaProvider provider = Build(FakeHttpTransport.JsonByPath(
            ("/api/tags", threeModels),
            ("nomic-embed-text", ShowEmbedding),
            ("/api/show", ShowCompletion)));

        Result<ModelInfo> info = await provider.GetModelInfoAsync(default);

        Assert.True(info.IsSuccess);
        Assert.Equal("mistral:7b-instruct", info.Value.ModelId);
    }

    /// <summary>
    /// With nothing but embedding models installed, the server has no chat model and says so.
    /// </summary>
    /// <remarks>
    /// Returning the embedding model anyway is what produced the original failure — a stored
    /// model id that only fails later, at the point someone tries to use it.
    /// </remarks>
    [Fact]
    public async Task A_Server_With_Only_Embedding_Models_Reports_No_Chat_Model()
    {
        const string embeddingOnly = """
            {
              "models": [
                { "name": "nomic-embed-text:latest", "model": "nomic-embed-text:latest",
                  "details": { "family": "nomic-bert" } }
              ]
            }
            """;

        OllamaProvider provider = Build(FakeHttpTransport.JsonByPath(
            ("/api/tags", embeddingOnly),
            ("/api/show", ShowEmbedding)));

        Result<ModelInfo> info = await provider.GetModelInfoAsync(default);

        Assert.True(info.IsFailure);
        Assert.Contains("embedding", info.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An older server that does not report capabilities has nothing filtered out.
    /// </summary>
    /// <remarks>
    /// <c>capabilities</c> is not in every Ollama version. Treating a silent server as
    /// "everything is an embedding model" would take working setups offline over a missing
    /// field, so the absence of an answer means no filtering rather than no models.
    /// </remarks>
    [Fact]
    public async Task A_Server_That_Does_Not_Report_Capabilities_Filters_Nothing()
    {
        OllamaProvider provider = Build(FakeHttpTransport.JsonByPath(
            ("/api/tags", TagsWithALargeEmbeddingModel),
            ("/api/show", """{ "license": "apache" }""")));

        Result<ModelInfo> info = await provider.GetModelInfoAsync(default);

        Assert.True(info.IsSuccess);
        Assert.Equal("big-embed:latest", info.Value.ModelId);
    }

    /// <summary>
    /// A server that has no <c>/api/show</c> at all is also not a reason to report nothing.
    /// </summary>
    [Fact]
    public async Task A_Server_Without_The_Show_Endpoint_Filters_Nothing()
    {
        // Only /api/tags is routed, so /api/show answers 404.
        OllamaProvider provider = Build(
            FakeHttpTransport.JsonByPath(("/api/tags", TagsWithALargeEmbeddingModel)));

        Result<ModelInfo> info = await provider.GetModelInfoAsync(default);

        Assert.True(info.IsSuccess);
        Assert.Equal("big-embed:latest", info.Value.ModelId);
    }

    /// <summary>
    /// When the server does answer, the largest model being an embedding model is no protection.
    /// </summary>
    [Fact]
    public async Task A_Large_Embedding_Model_Is_Still_Skipped()
    {
        OllamaProvider provider = Build(FakeHttpTransport.JsonByPath(
            ("/api/tags", TagsWithALargeEmbeddingModel),
            ("big-embed", ShowEmbedding),
            ("/api/show", ShowCompletion)));

        Result<ModelInfo> info = await provider.GetModelInfoAsync(default);

        Assert.True(info.IsSuccess);
        Assert.Equal("mistral:7b-instruct", info.Value.ModelId);
    }

    /// <summary>
    /// A server with no models at all is still an error, unchanged.
    /// </summary>
    [Fact]
    public async Task A_Server_With_No_Models_Reports_None()
    {
        OllamaProvider provider = Build(FakeHttpTransport.JsonByPath(
            ("/api/tags", """{ "models": [] }""")));

        Result<ModelInfo> info = await provider.GetModelInfoAsync(default);

        Assert.True(info.IsFailure);
    }

    private static OllamaProvider Build(FakeHttpTransport transport)
        => new(
            transport.CreateClient("ollama"),
            "test-ollama",
            "http://localhost:11434",
            NullLogger<OllamaProvider>.Instance);
}

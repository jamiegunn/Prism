using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference.Models;
using Prism.Common.Inference.Providers;
using Prism.Common.Results;
using Prism.Tests.Support;

namespace Prism.Tests.Unit.Inference;

/// <summary>
/// Covers Ollama's per-token probabilities, which Prism used to declare impossible.
/// </summary>
/// <remarks>
/// <para>
/// Ollama added logprobs in 0.12.11. Until then Prism hard-coded <c>SupportsLogprobs = false</c>
/// and told users that token-level introspection needed vLLM — which cannot run on Apple
/// Silicon at all, since it needs CUDA. So on a Mac the advice was to install something
/// unavailable in order to get something already working.
/// </para>
/// <para>
/// The bodies below are real responses recorded from Ollama 0.32.6 on
/// <c>/api/chat</c>, trimmed to the fields the parser reads. Recording them keeps the
/// wire contract pinned without needing a server in CI; the companion live test in
/// <c>Integration/OllamaLogprobsTests</c> is what catches Ollama changing shape.
/// </para>
/// </remarks>
public sealed class OllamaLogprobsTests
{
    /// <summary>A recorded non-streaming reply carrying logprobs and alternatives.</summary>
    private const string ChatWithLogprobs = """
        {
          "model": "mistral:7b-instruct",
          "message": { "role": "assistant", "content": " Hello" },
          "done": true,
          "done_reason": "stop",
          "prompt_eval_count": 10,
          "eval_count": 2,
          "total_duration": 1234567890,
          "eval_duration": 987654321,
          "logprobs": [
            {
              "token": " Hello",
              "logprob": -0.02698088251054287,
              "bytes": [32, 72, 101, 108, 108, 111],
              "top_logprobs": [
                { "token": " Hello", "logprob": -0.02698088251054287, "bytes": [32, 72, 101, 108, 108, 111] },
                { "token": " Hi", "logprob": -3.742387294769287, "bytes": [32, 72, 105] }
              ]
            }
          ]
        }
        """;

    /// <summary>The same reply from a server too old to know the parameter.</summary>
    private const string ChatWithoutLogprobs = """
        {
          "model": "mistral:7b-instruct",
          "message": { "role": "assistant", "content": " Hello" },
          "done": true,
          "done_reason": "stop",
          "prompt_eval_count": 10,
          "eval_count": 2,
          "total_duration": 1234567890,
          "eval_duration": 987654321
        }
        """;

    /// <summary>
    /// The recorded shape survives the round trip into Prism's provider-agnostic model.
    /// </summary>
    [Fact]
    public async Task Logprobs_And_Their_Alternatives_Are_Parsed()
    {
        (OllamaProvider provider, FakeHttpTransport _) = Build(ChatWithLogprobs);

        Result<ChatResponse> response = await provider.ChatAsync(Ask(topLogprobs: 2), default);

        Assert.True(response.IsSuccess);

        LogprobsData? logprobs = response.Value.LogprobsData;
        Assert.NotNull(logprobs);

        TokenLogprob token = Assert.Single(logprobs!.Tokens);
        Assert.Equal(" Hello", token.Token);
        Assert.Equal(-0.02698088251054287, token.Logprob, precision: 10);

        // exp(-0.027) — the check that catches a probability read as a log probability.
        Assert.Equal(0.9733, token.Probability, precision: 3);

        Assert.Collection(
            token.TopLogprobs,
            best => Assert.Equal(" Hello", best.Token),
            second =>
            {
                Assert.Equal(" Hi", second.Token);
                Assert.True(second.Logprob < token.Logprob, "Alternatives rank below the pick.");
            });
    }

    /// <summary>
    /// A server that returns no logprobs yields null rather than an empty shell.
    /// </summary>
    /// <remarks>
    /// An empty <see cref="LogprobsData"/> would make the UI believe it had data and render an
    /// empty heatmap, which is the failure this whole area keeps producing.
    /// </remarks>
    [Fact]
    public async Task An_Absent_Logprobs_Field_Yields_Null()
    {
        (OllamaProvider provider, FakeHttpTransport _) = Build(ChatWithoutLogprobs);

        Result<ChatResponse> response = await provider.ChatAsync(Ask(topLogprobs: 2), default);

        Assert.True(response.IsSuccess);
        Assert.Null(response.Value.LogprobsData);
    }

    /// <summary>
    /// Streaming carries one entry per chunk, which is what the live heatmap consumes.
    /// </summary>
    [Fact]
    public async Task Each_Streamed_Chunk_Carries_Its_Own_Logprob()
    {
        const string stream = """
            {"message":{"role":"assistant","content":" Hello"},"done":false,"logprobs":[{"token":" Hello","logprob":-0.027,"top_logprobs":[{"token":" Hello","logprob":-0.027},{"token":" Hi","logprob":-3.74}]}]}
            {"message":{"role":"assistant","content":"!"},"done":false,"logprobs":[{"token":"!","logprob":-0.5,"top_logprobs":[{"token":"!","logprob":-0.5},{"token":",","logprob":-2.1}]}]}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":10,"eval_count":2}
            """;

        (OllamaProvider provider, FakeHttpTransport _) = Build(stream);

        List<StreamChunk> chunks = [];

        await foreach (StreamChunk chunk in provider.StreamChatAsync(Ask(topLogprobs: 2), default))
        {
            chunks.Add(chunk);
        }

        List<StreamChunk> withContent = [.. chunks.Where(c => c.Content.Length > 0)];

        Assert.Equal(2, withContent.Count);
        Assert.All(withContent, c => Assert.NotNull(c.LogprobsEntry));
        Assert.Equal(" Hello", withContent[0].LogprobsEntry!.Token);
        Assert.Equal("!", withContent[1].LogprobsEntry!.Token);
        Assert.Equal(2, withContent[0].LogprobsEntry!.TopLogprobs.Count);
    }

    /// <summary>
    /// The parameters go at the top level, where Ollama reads them.
    /// </summary>
    /// <remarks>
    /// Putting them in <c>options</c> alongside temperature is the obvious guess and returns no
    /// logprobs at all, with no error — so this asserts placement, not just presence.
    /// </remarks>
    [Fact]
    public async Task Logprobs_Are_Requested_At_The_Top_Level_Not_In_Options()
    {
        (OllamaProvider provider, FakeHttpTransport transport) = Build(ChatWithLogprobs);

        await provider.ChatAsync(Ask(topLogprobs: 3), default);

        JsonNode body = JsonNode.Parse(transport.RequestBodies[0])!;

        Assert.True(body["logprobs"]!.GetValue<bool>());
        Assert.Equal(3, body["top_logprobs"]!.GetValue<int>());
        Assert.Null(body["options"]?["logprobs"]);
        Assert.Null(body["options"]?["top_logprobs"]);
    }

    /// <summary>
    /// Ollama rejects a top-K above 20 outright, so the value is clamped before it is sent.
    /// </summary>
    [Fact]
    public async Task A_Top_K_Beyond_The_Servers_Limit_Is_Clamped()
    {
        (OllamaProvider provider, FakeHttpTransport transport) = Build(ChatWithLogprobs);

        await provider.ChatAsync(Ask(topLogprobs: 500), default);

        JsonNode body = JsonNode.Parse(transport.RequestBodies[0])!;

        Assert.Equal(20, body["top_logprobs"]!.GetValue<int>());
    }

    /// <summary>
    /// Nothing is requested when the caller did not ask, so ordinary chats stay unchanged.
    /// </summary>
    [Fact]
    public async Task Nothing_Is_Requested_When_Logprobs_Are_Not_Wanted()
    {
        (OllamaProvider provider, FakeHttpTransport transport) = Build(ChatWithLogprobs);

        await provider.ChatAsync(
            new ChatRequest { Model = "mistral:7b-instruct", Messages = [ChatMessage.User("Hi")] },
            default);

        JsonNode body = JsonNode.Parse(transport.RequestBodies[0])!;

        Assert.Null(body["logprobs"]);
        Assert.Null(body["top_logprobs"]);
    }

    /// <summary>
    /// The advertised capability describes the server in front of us, by version.
    /// </summary>
    /// <param name="version">The version the server reports.</param>
    /// <param name="expected">Whether logprobs should be offered.</param>
    [Theory]
    [InlineData("0.32.6", true)]
    [InlineData("0.12.11", true)]
    [InlineData("0.12.10", false)]
    [InlineData("0.9.0", false)]
    public async Task Capabilities_Follow_The_Servers_Version(string version, bool expected)
    {
        (OllamaProvider provider, FakeHttpTransport _) = Build($$"""{"version":"{{version}}"}""");

        await provider.CheckHealthAsync(default);

        Assert.Equal(expected, provider.Capabilities.SupportsLogprobs);

        // The two must move together, or the UI shows a top-K slider with nothing behind it.
        Assert.Equal(expected, provider.Capabilities.MaxTopLogprobs > 0);
    }

    /// <summary>
    /// An unreadable version keeps the feature on rather than hiding a working one.
    /// </summary>
    [Fact]
    public async Task An_Unreadable_Version_Does_Not_Hide_Working_Features()
    {
        (OllamaProvider provider, FakeHttpTransport _) = Build("""{"version":"not-a-version"}""");

        await provider.CheckHealthAsync(default);

        Assert.True(provider.Capabilities.SupportsLogprobs);
    }

    private static (OllamaProvider Provider, FakeHttpTransport Transport) Build(string responseBody)
    {
        FakeHttpTransport transport = FakeHttpTransport.Json(responseBody);

        OllamaProvider provider = new(
            transport.CreateClient("ollama"),
            "test-ollama",
            "http://localhost:11434",
            NullLogger<OllamaProvider>.Instance);

        return (provider, transport);
    }

    private static ChatRequest Ask(int topLogprobs) => new()
    {
        Model = "mistral:7b-instruct",
        Messages = [ChatMessage.User("Say hello.")],
        MaxTokens = 5,
        Logprobs = true,
        TopLogprobs = topLogprobs
    };
}

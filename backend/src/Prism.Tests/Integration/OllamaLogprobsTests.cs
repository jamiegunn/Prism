using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference.Models;
using Prism.Common.Inference.Providers;
using Prism.Common.Results;

namespace Prism.Tests.Integration;

/// <summary>
/// Checks Ollama's logprobs against a real server rather than a stub.
/// </summary>
/// <remarks>
/// <para>
/// Prism spent a long time asserting that Ollama returned no per-token probabilities, and told
/// users to run vLLM instead — advice that cannot be followed on an Apple Silicon Mac, where
/// vLLM needs CUDA. Ollama added logprobs in 0.12.11, so the assertion quietly became false and
/// left the heatmap, entropy chart and Token Explorer switched off on servers that support them.
/// </para>
/// <para>
/// A mock cannot catch that: the belief was about the wire format, so only a live server can
/// confirm it. These skip when nothing is listening on 11434, so they cost nothing in CI and
/// fail loudly on a developer machine if Ollama's response shape ever moves.
/// </para>
/// </remarks>
public sealed class OllamaLogprobsTests
{
    private const string Endpoint = "http://localhost:11434";

    /// <summary>
    /// Set <c>PRISM_REQUIRE_OLLAMA=1</c> to turn "no server, nothing to check" into a failure.
    /// </summary>
    /// <remarks>
    /// A test that silently passes when it could not run is worse than no test: these finish in
    /// milliseconds when they skip, which reads exactly like a fast pass. This switch is how you
    /// prove they really exercised a server.
    /// </remarks>
    private static bool RequireServer =>
        Environment.GetEnvironmentVariable("PRISM_REQUIRE_OLLAMA") == "1";

    /// <summary>
    /// The wire fields the parser depends on, present with alternatives attached.
    /// </summary>
    [Fact]
    public async Task A_Live_Ollama_Returns_Logprobs_With_Alternatives()
    {
        (OllamaProvider? provider, string? model) = await ConnectAsync();

        if (provider is null || model is null)
        {
            return;
        }

        Result<ChatResponse> response = await provider.ChatAsync(
            new ChatRequest
            {
                Model = model,
                Messages = [ChatMessage.User("Say hello.")],
                MaxTokens = 5,
                Logprobs = true,
                TopLogprobs = 3
            },
            CancellationToken.None);

        // Built only on failure: the interpolated string is evaluated before Assert.True runs,
        // and reading Error on a successful Result throws — so an eager message turns every
        // pass into an error.
        if (response.IsFailure)
        {
            Assert.Fail($"Ollama chat failed: {response.Error.Message}");
        }

        LogprobsData? logprobs = response.Value.LogprobsData;

        Assert.NotNull(logprobs);
        Assert.NotEmpty(logprobs!.Tokens);

        TokenLogprob first = logprobs.Tokens[0];

        Assert.False(string.IsNullOrEmpty(first.Token));

        // A log probability is negative, and exp() of it has to land in (0, 1] — the check that
        // catches a value read out of the wrong field.
        Assert.True(first.Logprob <= 0, $"Expected a log probability, got {first.Logprob}");
        Assert.InRange(first.Probability, 0d, 1d);

        // Alternatives are what the Token Explorer draws; without them it has nothing to show.
        Assert.NotEmpty(first.TopLogprobs);
        Assert.All(first.TopLogprobs, alternative =>
        {
            Assert.False(string.IsNullOrEmpty(alternative.Token));
            Assert.True(alternative.Logprob <= 0);
        });
    }

    /// <summary>
    /// The streaming path carries one entry per chunk, which is what the heatmap consumes.
    /// </summary>
    [Fact]
    public async Task Streaming_Carries_A_Logprob_Per_Chunk()
    {
        (OllamaProvider? provider, string? model) = await ConnectAsync();

        if (provider is null || model is null)
        {
            return;
        }

        int chunksWithContent = 0;
        int chunksWithLogprobs = 0;

        await foreach (StreamChunk chunk in provider.StreamChatAsync(
            new ChatRequest
            {
                Model = model,
                Messages = [ChatMessage.User("Say hello.")],
                MaxTokens = 5,
                Logprobs = true,
                TopLogprobs = 2
            },
            CancellationToken.None))
        {
            if (chunk.Content.Length > 0)
            {
                chunksWithContent++;

                if (chunk.LogprobsEntry is not null)
                {
                    chunksWithLogprobs++;
                }
            }
        }

        Assert.True(chunksWithContent > 0, "The server streamed no content at all.");
        Assert.Equal(chunksWithContent, chunksWithLogprobs);
    }

    /// <summary>
    /// A health check settles the capability, so registration persists the right answer.
    /// </summary>
    [Fact]
    public async Task Checking_Health_Settles_Whether_Logprobs_Are_Offered()
    {
        (OllamaProvider? provider, string? _) = await ConnectAsync();

        if (provider is null)
        {
            return;
        }

        // ConnectAsync has already health-checked, which is where the version is read.
        Assert.True(
            provider.Capabilities.SupportsLogprobs,
            "A current Ollama returns logprobs; capabilities must say so or the UI hides the "
            + "token-level views.");

        Assert.True(provider.Capabilities.MaxTopLogprobs > 0);
    }

    /// <summary>
    /// Connects to a local Ollama and finds a model to talk to.
    /// </summary>
    /// <returns>The provider and model, or nulls when no server is available.</returns>
    private static async Task<(OllamaProvider? Provider, string? Model)> ConnectAsync()
    {
        using HttpClient probe = new() { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            using HttpResponseMessage tags = await probe.GetAsync($"{Endpoint}/api/tags");

            if (!tags.IsSuccessStatusCode)
            {
                return Unavailable($"{Endpoint}/api/tags returned {(int)tags.StatusCode}");
            }

            string body = await tags.Content.ReadAsStringAsync();
            string? model = JsonNode.Parse(body)?["models"]?.AsArray()
                .FirstOrDefault()?["name"]?.GetValue<string>();

            if (model is null)
            {
                return Unavailable("Ollama is running but has no models pulled.");
            }

            // Generation on a CPU-only container is slow; this is not a timing test.
            HttpClient client = new() { Timeout = TimeSpan.FromMinutes(5) };
            OllamaProvider provider = new(
                client, "live-ollama", Endpoint, NullLogger<OllamaProvider>.Instance);

            await provider.CheckHealthAsync(CancellationToken.None);

            return (provider, model);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Unavailable(ex.Message);
        }
    }

    /// <summary>
    /// Reports that no server was reachable, failing instead when the caller demanded one.
    /// </summary>
    /// <param name="reason">Why the connection did not happen.</param>
    /// <returns>A pair of nulls, when not configured to fail.</returns>
    private static (OllamaProvider? Provider, string? Model) Unavailable(string reason)
    {
        Assert.False(
            RequireServer,
            $"PRISM_REQUIRE_OLLAMA=1 but no usable Ollama on {Endpoint}: {reason}");

        return (null, null);
    }
}

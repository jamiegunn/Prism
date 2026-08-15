using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference.Providers;
using Prism.Common.Results;
using Prism.Tests.Support;

namespace Prism.Tests.Unit.Inference;

/// <summary>
/// Covers loading a model on Ollama, and noticing when it did not load.
/// </summary>
/// <remarks>
/// <para>
/// <c>/api/pull</c> answers 200 and then streams its progress as NDJSON, and that is where
/// failure appears: <c>{"error":"pull model manifest: file does not exist"}</c> arrives *inside*
/// a response whose status code already said OK. The provider checked the status code, read the
/// stream to the end discarding every line, and returned success.
/// </para>
/// <para>
/// So swapping an instance to a model that does not exist — a typo, a model never pulled — came
/// back 200, and the instance was recorded as running it. Nothing failed until the next attempt
/// to generate anything, by which time the screen had been showing the wrong model as current
/// for however long.
/// </para>
/// </remarks>
public sealed class OllamaModelLoadTests
{
    /// <summary>A pull that fails, exactly as Ollama 0.32.6 streams it.</summary>
    private const string FailedPull =
        """
        {"status":"pulling manifest"}
        {"error":"pull model manifest: file does not exist"}
        """;

    /// <summary>A pull that succeeds, ending in the terminal status.</summary>
    private const string SucceededPull =
        """
        {"status":"pulling manifest"}
        {"status":"downloading","digest":"sha256:aa","total":100,"completed":100}
        {"status":"verifying sha256 digest"}
        {"status":"writing manifest"}
        {"status":"success"}
        """;

    /// <summary>
    /// An error inside the stream is a failure, whatever the status code said.
    /// </summary>
    [Fact]
    public async Task An_Error_In_The_Pull_Stream_Is_A_Failure()
    {
        OllamaProvider provider = Build(FailedPull);

        Result result = await provider.LoadModelAsync("totally-made-up-model:99b", default);

        Assert.True(result.IsFailure);
        Assert.Contains("file does not exist", result.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A stream that reaches "success" is a success.
    /// </summary>
    [Fact]
    public async Task A_Completed_Pull_Succeeds()
    {
        OllamaProvider provider = Build(SucceededPull);

        Result result = await provider.LoadModelAsync("mistral:7b-instruct", default);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
    }

    /// <summary>
    /// A stream that stops before saying it succeeded is not a success either.
    /// </summary>
    /// <remarks>
    /// A pull interrupted partway leaves the model incomplete, and the old code could not tell
    /// that apart from a finished one: both are "the stream ended".
    /// </remarks>
    [Fact]
    public async Task A_Truncated_Pull_Does_Not_Count_As_Loaded()
    {
        OllamaProvider provider = Build(
            """
            {"status":"pulling manifest"}
            {"status":"downloading","digest":"sha256:aa","total":100,"completed":40}
            """);

        Result result = await provider.LoadModelAsync("mistral:7b-instruct", default);

        Assert.True(result.IsFailure);
    }

    /// <summary>
    /// A non-200 is still a failure, and still says what the server said.
    /// </summary>
    [Fact]
    public async Task A_Refused_Pull_Is_A_Failure()
    {
        FakeHttpTransport transport = FakeHttpTransport.Refuses(
            HttpStatusCode.BadRequest, """{"error":"invalid model name"}""");

        OllamaProvider provider = new(
            transport.CreateClient("ollama"),
            "test-ollama",
            "http://localhost:11434",
            NullLogger<OllamaProvider>.Instance);

        Result result = await provider.LoadModelAsync("", default);

        Assert.True(result.IsFailure);
        Assert.Contains("invalid model name", result.Error.Message, StringComparison.Ordinal);
    }

    private static OllamaProvider Build(string pullStream)
    {
        FakeHttpTransport transport = FakeHttpTransport.Json(pullStream);

        return new OllamaProvider(
            transport.CreateClient("ollama"),
            "test-ollama",
            "http://localhost:11434",
            NullLogger<OllamaProvider>.Instance);
    }
}

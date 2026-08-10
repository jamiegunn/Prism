using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.Models.Application;
using Prism.Tests.Support;

namespace Prism.Tests.Unit.Inference;

/// <summary>
/// Covers the wire format each provider receives for guided decoding.
/// </summary>
/// <remarks>
/// Every provider was previously sent the bare JSON Schema as <c>response_format</c>. vLLM
/// constrains generation through <c>guided_json</c>, the OpenAI API expects a wrapped
/// <c>json_schema</c> response format, and Ollama takes the schema as <c>format</c> — so none
/// of them received something they act on, and guided decoding silently did nothing while
/// appearing configured.
/// </remarks>
public sealed class GuidedDecodingPayloadTests
{
    private const string Schema = """{"type":"object","properties":{"a":{"type":"string"}}}""";

    /// <summary>
    /// vLLM applies its grammar backend from guided_json, and ignores response_format for this.
    /// </summary>
    [Fact]
    public async Task Vllm_Receives_GuidedJson_And_Not_A_Bare_ResponseFormat()
    {
        JsonElement body = await CaptureRequestAsync(InferenceProviderType.Vllm);

        Assert.True(body.TryGetProperty("guided_json", out JsonElement guided));
        Assert.Equal("object", guided.GetProperty("type").GetString());
        Assert.False(body.TryGetProperty("response_format", out _));
    }

    /// <summary>
    /// The OpenAI wire format nests the schema under json_schema. A bare schema is rejected or
    /// ignored by every implementation of that API.
    /// </summary>
    [Fact]
    public async Task OpenAiCompatible_Receives_A_Wrapped_Json_Schema()
    {
        JsonElement body = await CaptureRequestAsync(InferenceProviderType.OpenAiCompatible);

        JsonElement format = body.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());

        JsonElement wrapper = format.GetProperty("json_schema");
        Assert.True(wrapper.TryGetProperty("name", out _));
        Assert.Equal("object", wrapper.GetProperty("schema").GetProperty("type").GetString());
    }

    /// <summary>
    /// Ollama takes the schema directly, with no wrapper.
    /// </summary>
    [Fact]
    public async Task Ollama_Receives_The_Schema_As_Format()
    {
        JsonElement body = await CaptureRequestAsync(InferenceProviderType.Ollama);

        Assert.True(body.TryGetProperty("format", out JsonElement format));
        Assert.Equal("object", format.GetProperty("type").GetString());
    }

    /// <summary>
    /// Without a schema, no guidance keys appear at all — a request that is not asking for
    /// structure must not carry the machinery for it.
    /// </summary>
    [Theory]
    [InlineData(InferenceProviderType.Vllm)]
    [InlineData(InferenceProviderType.OpenAiCompatible)]
    [InlineData(InferenceProviderType.Ollama)]
    public async Task No_Schema_Means_No_Guidance_Keys(InferenceProviderType providerType)
    {
        JsonElement body = await CaptureRequestAsync(providerType, schema: null);

        Assert.False(body.TryGetProperty("guided_json", out _));
        Assert.False(body.TryGetProperty("response_format", out _));
        Assert.False(body.TryGetProperty("format", out _));
    }

    /// <summary>
    /// The mode selector still works and is not confused with a schema.
    /// </summary>
    [Fact]
    public async Task A_Mode_Selector_Is_Sent_As_A_Type_Object()
    {
        JsonElement body = await CaptureRequestAsync(
            InferenceProviderType.OpenAiCompatible, schema: null, responseFormat: "json_object");

        Assert.Equal("json_object", body.GetProperty("response_format").GetProperty("type").GetString());
    }

    /// <summary>
    /// Capability reporting must match what the providers can actually do, since the handler
    /// gates native guidance on it and falls back to instructions otherwise.
    ///
    /// Ollama constrains generation with `format` from 0.5.0, verified against a live server:
    /// asking for a person against a two-field schema returns exactly that object. This test
    /// previously asserted false, which is what kept Structured Output on the fallback path and
    /// telling every Ollama user the schema could not be enforced.
    /// </summary>
    [Theory]
    [InlineData(InferenceProviderType.Vllm, true)]
    [InlineData(InferenceProviderType.OpenAiCompatible, false)]
    [InlineData(InferenceProviderType.Ollama, true)]
    public void Guided_Decoding_Capability_Is_Reported_Honestly(
        InferenceProviderType providerType, bool expected)
    {
        var factory = new InferenceProviderFactory(
            FakeHttpTransport.ChatCompletion("{}"), NullLoggerFactory.Instance);

        IInferenceProvider provider = factory.CreateProvider(
            "p", "http://localhost:9999", providerType);

        Assert.Equal(expected, provider.Capabilities.SupportsGuidedDecoding);
    }

    private static async Task<JsonElement> CaptureRequestAsync(
        InferenceProviderType providerType,
        string? schema = Schema,
        string? responseFormat = null)
    {
        FakeHttpTransport transport = providerType == InferenceProviderType.Ollama
            ? FakeHttpTransport.Json("""{"message":{"role":"assistant","content":"{}"},"done":true}""")
            : FakeHttpTransport.ChatCompletion("{}");

        var factory = new InferenceProviderFactory(transport, NullLoggerFactory.Instance);

        IInferenceProvider provider = factory.CreateProvider(
            "p", "http://localhost:9999", providerType);

        await provider.ChatAsync(
            new ChatRequest
            {
                Model = "m",
                Messages = [ChatMessage.User("hi")],
                JsonSchema = schema,
                ResponseFormat = responseFormat,
            },
            CancellationToken.None);

        return JsonDocument.Parse(transport.RequestBodies[^1]).RootElement.Clone();
    }
}

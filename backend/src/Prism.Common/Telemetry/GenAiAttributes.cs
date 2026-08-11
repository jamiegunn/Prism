namespace Prism.Common.Telemetry;

/// <summary>
/// The OpenTelemetry GenAI semantic-convention attribute names Prism emits, as constants so
/// tests assert against the convention rather than against string literals scattered through
/// the instrumentation. Names follow the OpenTelemetry GenAI semantic conventions
/// (<c>gen_ai.*</c>), which is what lets Jaeger, Langfuse and Phoenix read Prism's traces
/// without translation.
/// </summary>
public static class GenAiAttributes
{
    /// <summary>The GenAI system (provider family), e.g. <c>ollama</c> or <c>vllm</c>.</summary>
    public const string System = "gen_ai.system";

    /// <summary>The operation, e.g. <c>chat</c>.</summary>
    public const string OperationName = "gen_ai.operation.name";

    /// <summary>The model the request named.</summary>
    public const string RequestModel = "gen_ai.request.model";

    /// <summary>The sampling temperature requested.</summary>
    public const string RequestTemperature = "gen_ai.request.temperature";

    /// <summary>The nucleus-sampling parameter requested.</summary>
    public const string RequestTopP = "gen_ai.request.top_p";

    /// <summary>The maximum output tokens requested.</summary>
    public const string RequestMaxTokens = "gen_ai.request.max_tokens";

    /// <summary>The model the response reports having used.</summary>
    public const string ResponseModel = "gen_ai.response.model";

    /// <summary>The finish reasons of the response's choices.</summary>
    public const string ResponseFinishReasons = "gen_ai.response.finish_reasons";

    /// <summary>Prompt token count.</summary>
    public const string UsageInputTokens = "gen_ai.usage.input_tokens";

    /// <summary>Completion token count.</summary>
    public const string UsageOutputTokens = "gen_ai.usage.output_tokens";

    /// <summary>Standard OTel error-type attribute set on failed spans.</summary>
    public const string ErrorType = "error.type";

    /// <summary>
    /// The full prompt content. Sensitive; emitted only when content capture is explicitly
    /// enabled — the convention itself makes content opt-in.
    /// </summary>
    public const string PromptContent = "gen_ai.prompt";

    /// <summary>
    /// The full completion content. Sensitive; emitted only when content capture is
    /// explicitly enabled.
    /// </summary>
    public const string CompletionContent = "gen_ai.completion";
}

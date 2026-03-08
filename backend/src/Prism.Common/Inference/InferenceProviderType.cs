namespace Prism.Common.Inference;

/// <summary>
/// Identifies the type of inference provider backend.
/// </summary>
public enum InferenceProviderType
{
    /// <summary>vLLM inference server with OpenAI-compatible API and extended metrics.</summary>
    Vllm,

    /// <summary>Ollama local inference with its own REST API.</summary>
    Ollama,

    /// <summary>LM Studio with OpenAI-compatible API.</summary>
    LmStudio,

    /// <summary>Generic OpenAI-compatible inference server.</summary>
    OpenAiCompatible
}

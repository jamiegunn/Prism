# ADR-006: Inference Provider Abstraction

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The platform's core function is interacting with LLM inference engines. Multiple backends exist:

- **vLLM** — high-performance, feature-rich (logprobs, guided decoding, prefix caching, LoRA, model swap)
- **Ollama** — easy local setup, growing feature set
- **LM Studio** — GUI-friendly local inference
- **OpenAI-compatible APIs** — any server implementing the OpenAI chat/completions spec

Locking to a single backend limits the platform's usefulness as a research tool. Researchers need to compare behavior across engines, switch backends based on available hardware, and use the best tool for each task.

## Decision

Introduce `IInferenceProvider` with a capability-based design:

```csharp
public interface IInferenceProvider
{
    string ProviderId { get; }
    string ProviderType { get; }
    ProviderCapabilities Capabilities { get; }

    Task<Result<ChatCompletionResponse>> ChatCompletionAsync(ChatCompletionRequest request, CancellationToken ct);
    Task<Result<CompletionResponse>> CompletionAsync(CompletionRequest request, CancellationToken ct);
    IAsyncEnumerable<Result<StreamChunk>> StreamChatCompletionAsync(ChatCompletionRequest request, CancellationToken ct);
    Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(CancellationToken ct);
    Task<Result<TokenizeResponse>> TokenizeAsync(TokenizeRequest request, CancellationToken ct);
    Task<Result<HealthStatus>> HealthCheckAsync(CancellationToken ct);
}
```

`ProviderCapabilities` declares what each backend supports:

```csharp
public record ProviderCapabilities(
    bool SupportsLogprobs,
    bool SupportsGuidedDecoding,
    bool SupportsStreaming,
    bool SupportsMetrics,
    bool SupportsTokenize,
    bool SupportsMultimodal,
    bool SupportsLoRA,
    bool SupportsPrefixCaching,
    bool SupportsModelSwap
);
```

Provider capability matrix:

| Capability | vLLM | Ollama | LM Studio | OpenAI-compat |
|-----------|------|--------|-----------|---------------|
| Logprobs | Yes | Partial | No | Varies |
| Guided decoding | Yes | No | No | No |
| Streaming | Yes | Yes | Yes | Yes |
| Tokenize | Yes | No | No | No |
| Model swap | Yes | Yes | No | No |
| LoRA | Yes | No | No | No |
| Prefix caching | Yes | No | No | No |

Features check `Capabilities` before calling optional operations and degrade gracefully.

### Provider Registry

`IInferenceProviderFactory` manages runtime registration:

```csharp
public interface IInferenceProviderFactory
{
    IInferenceProvider GetProvider(string providerId);
    IInferenceProvider GetDefaultProvider();
    IReadOnlyList<ProviderInfo> ListProviders();
    void RegisterProvider(ProviderConfig config);
    void UnregisterProvider(string providerId);
    void UpdateProvider(string providerId, ProviderConfig config);
    void ReloadFromConfig();
}
```

Providers are configured via `providers.json` with `FileSystemWatcher` for hot-reload, and via runtime API endpoints (`POST /api/v1/providers`, `PUT /api/v1/providers/{id}`).

### Inference Recording

`RecordingInferenceProvider` is a decorator that wraps any `IInferenceProvider` and automatically captures every inference call as an `InferenceRecord` for history and replay (see ADR-001, DESIGN.md History & Replay section).

### Hot Reload

`IHotReloadableProvider` extends `IInferenceProvider` for backends that support model swapping:

```csharp
public interface IHotReloadableProvider : IInferenceProvider
{
    Task<Result<ModelSwapResult>> SwapModelAsync(string modelId, ModelSwapOptions? options, CancellationToken ct);
    Task<Result<string>> GetLoadedModelAsync(CancellationToken ct);
}
```

## Consequences

### Positive

- Feature code depends only on `IInferenceProvider` — add new backends without touching features
- Capability-based degradation: features adapt their UI based on what the active provider supports
- Provider swap at runtime without restart — via config file change or API call
- Recording decorator captures full inference history transparently
- Research workflows can compare the same prompt across different backends

### Negative

- Lowest-common-denominator risk: features limited by the weakest provider
  - Mitigated by capability checks — features show/hide based on `Capabilities`
- Each new provider requires implementing the full interface
- Config-driven hot-reload adds complexity (file watchers, thread safety)

### Neutral

- `providers.json` is the source of truth for provider configuration
- Default provider is marked in config; fallback if default goes unhealthy
- Provider health checks run on a configurable interval

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| vLLM only | Simplest, richest feature set | Locks out researchers using Ollama/LM Studio | Limits platform adoption and flexibility |
| Separate codebase per backend | Maximum optimization per backend | Massive duplication, divergent features | Unmaintainable |
| LiteLLM as intermediary | Proxy handles provider differences | Extra dependency, less control, hides capabilities | Obscures provider-specific features (logprobs, guided decoding) we need for research |

## References

- See `ARCHITECTURE.md` — Inference Provider Abstraction, Provider Registry, and Inference History sections
- See `DESIGN.md` — Model Management and History & Replay modules

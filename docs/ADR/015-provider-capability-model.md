# ADR-015: Provider Capability Model

**Date:** 2026-03-16
**Status:** Accepted
**Deciders:** Project team

## Context

Prism's README promises provider-agnostic behavior, but real providers vary significantly in what they support. vLLM exposes logprobs, tokenization, and guided decoding. Ollama supports logprobs but not guided decoding. LM Studio has different streaming behavior. OpenAI has its own constraints.

Without explicit capability modeling:
- UI shows controls that silently fail
- Features assume capabilities that don't exist
- Users can't tell what's supported without trial and error
- Code paths branch on provider names instead of capabilities

## Decision

Introduce a first-class provider capability model in `Prism.Common/Inference/Capabilities/`.

### Capability Tiers

| Tier | Capabilities | Typical Providers |
|------|-------------|-------------------|
| `TierResearch` | logprobs, tokenize, guided decoding, streaming, full metrics | vLLM with full config |
| `TierInspect` | tokenize, streaming, logprobs (possibly limited top-K), partial metrics | Ollama, some vLLM configs |
| `TierChat` | chat/completions, streaming | LM Studio, generic OpenAI-compatible |

### Capability Flags

Individual capabilities are represented as flags, not just tiers:

```csharp
public sealed record ProviderCapabilities
{
    public bool SupportsLogprobs { get; init; }
    public int? MaxLogprobs { get; init; }
    public bool SupportsTokenize { get; init; }
    public bool SupportsGuidedDecoding { get; init; }
    public bool SupportsStreaming { get; init; }
    public bool SupportsFunctionCalling { get; init; }
    public bool SupportsStop { get; init; }
    public bool SupportsTemperature { get; init; }
    public bool SupportsTopP { get; init; }
    public bool SupportsTopK { get; init; }
    public bool SupportsMaxTokens { get; init; }
    public bool SupportsModelListing { get; init; }
    public bool SupportsHealthCheck { get; init; }
    public CapabilityTier Tier { get; init; }
    public DateTimeOffset ProbedAt { get; init; }
}
```

### IProviderCapabilityRegistry

```csharp
public interface IProviderCapabilityRegistry
{
    Task<Result<ProviderCapabilities>> ProbeAsync(Guid providerInstanceId, CancellationToken ct);
    Task<Result<ProviderCapabilities>> GetCachedAsync(Guid providerInstanceId, CancellationToken ct);
    Task<Result> RefreshAsync(Guid providerInstanceId, CancellationToken ct);
    Task<Result<IReadOnlyList<ProviderCapabilitySummary>>> ListAllAsync(CancellationToken ct);
}
```

### Probing Strategy

1. **On registration:** probe immediately and persist results.
2. **Periodic refresh:** background refresh at configurable interval (default: 5 minutes).
3. **On-demand:** features can request a fresh probe.
4. **Graceful degradation:** if probing fails, mark capabilities as unknown, not absent.

### UI Rules

1. **Never show unsupported controls as enabled.** Disabled controls show a tooltip explaining why.
2. **Show inline "why unavailable" messages** when a feature requires capabilities the provider lacks.
3. **Offer best-effort fallback** when possible (e.g., client-side token counting when tokenize endpoint is unavailable).
4. **Capability badges** on provider cards and model selection dropdowns.

## Consequences

### Positive

- Users immediately see what's possible with their provider
- No silent failures or confusing error messages
- Features can declare required capabilities and get clean validation
- Provider comparison becomes meaningful — show capability matrix
- Adding new providers only requires implementing the interface and declaring capabilities

### Negative

- Probing adds latency on registration and periodic overhead
- Capability flags may not capture nuanced differences between providers
- UI complexity increases — every control needs a capability gate

### Neutral

- Tiers are convenience labels; individual flags are the source of truth
- New capabilities can be added as flags without changing the tier model
- Providers that don't support probing get `TierChat` as a safe default

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Provider name-based branching | Simple if-else | Fragile, doesn't handle version differences, grows unboundedly | Doesn't scale |
| User-declared capabilities | No probing needed | Error-prone, users don't know what their provider supports | Bad UX |
| Feature flags per provider | Explicit | Requires updating a config for every provider × feature combination | Maintenance burden |

## References

- ADR-006: Inference Provider Abstraction
- Delivery Plan v2: Section 4.C (Make capabilities first-class)

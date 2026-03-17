# ADR-013: Inference Runtime Layer

**Date:** 2026-03-16
**Status:** Accepted
**Deciders:** Project team

## Context

Multiple features (Playground, History, Experiments, Batch, RAG, Structured Output, Agents) all need to execute inference, record traces, compute token metrics, and handle retries/cancellation. Without a shared runtime, each feature reinvents this logic, leading to inconsistent recording, duplicated error handling, and divergent token analysis.

The existing `IInferenceProvider` abstraction handles the transport layer (send request, get response). What's missing is the orchestration layer above it: resolve provider/model, execute with recording, stream tokens, compute entropy/perplexity/surprise, emit artifacts, and centralize retries/timeouts/cancellation.

## Decision

Introduce a canonical inference runtime layer in `Prism.Common/Inference/Runtime/` with these abstractions:

### Core Interfaces

| Interface | Responsibility |
|-----------|---------------|
| `IInferenceRuntime` | Resolve provider + model, execute inference, stream tokens, return structured run result. Single entry point for all feature-level inference. |
| `IInferenceRecorder` | Persist `InferenceRun`, `InferenceTrace`, and `TokenEvent` records. Every runtime call is automatically recorded. |
| `ITokenAnalysisService` | Compute entropy, perplexity, surprise, calibration, and top-K analysis from token probability data. |
| `IReplayService` | Re-execute a recorded run with optional overrides (model, parameters, prompt). Produce a `ReplayRun` linked to the original. |
| `IProviderCapabilityRegistry` | Probe and cache provider capabilities (logprobs, tokenize, guided decoding, streaming). UI reads this to enable/disable controls. |

### Design Rules

1. **All user-facing inference routes call `IInferenceRuntime`** — no feature may call `IInferenceProvider` directly for user-initiated inference.
2. **Recording is mandatory** — the runtime always records. Features opt in to additional metadata (tags, project links, experiment associations).
3. **Token analysis is computed once** — the runtime computes metrics after inference and stores them with the trace. Features read computed metrics, never recompute.
4. **Capabilities are checked before execution** — the runtime validates that the requested features (logprobs, guided decoding, etc.) are supported by the target provider before sending the request.
5. **CancellationToken flows through the entire pipeline** — from HTTP request to provider call to recording.

### Runtime Pipeline

```
Feature Endpoint
  → IInferenceRuntime.ExecuteAsync(request, options, ct)
    → IProviderCapabilityRegistry.Validate(provider, requiredCapabilities)
    → IInferenceProvider.ChatCompletionAsync(request, ct)  [streaming or non-streaming]
    → IInferenceRecorder.RecordRunAsync(run, trace, ct)
    → ITokenAnalysisService.AnalyzeAsync(tokenEvents, ct)
    → Return InferenceRunResult { Run, Trace, Metrics }
```

## Consequences

### Positive

- Every module gets identical recording, metrics, and retry behavior
- Token analysis logic exists in one place — no drift between Playground and Token Explorer
- Replay is a first-class operation, not bolted on per feature
- Provider capability validation happens before wasting a call
- Easier to add cross-cutting concerns (rate limiting, cost tracking, audit) in one place

### Negative

- All features take a dependency on the runtime — changes to the runtime interface affect multiple modules
- The runtime must be carefully designed to avoid becoming a god object
- Streaming adds complexity to the recording pipeline

### Neutral

- Existing `IInferenceProvider` remains unchanged — it is the transport layer that the runtime orchestrates
- The `RecordingInferenceProvider` decorator may be replaced by runtime-level recording, but the pattern is compatible

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Keep per-feature inference calls | Features are independent | Duplicated recording, inconsistent metrics, replay logic in multiple places | Doesn't scale past 3 features |
| Middleware-only approach | Less coupling | Can't control recording granularity, can't compute token metrics at middleware level | Too coarse-grained for research features |
| Event-driven (publish inference events) | Loose coupling | Eventual consistency complicates UI, harder to debug, overhead for single-user tool | Over-engineered for local-first platform |

## References

- ADR-006: Inference Provider Abstraction (transport layer)
- ARCHITECTURE.md: Provider abstractions section
- Delivery Plan v2: Section 4.A (Canonical runtime layer)

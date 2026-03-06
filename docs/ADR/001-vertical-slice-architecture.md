# ADR-001: Vertical Slice Architecture

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The AI Research Workbench contains 14+ feature modules (Playground, Prompt Lab, Experiments, History & Replay, Model Management, RAG, Agents, etc.). Traditional layered architecture (Controllers -> Services -> Repositories) creates cross-cutting dependencies where a change to one feature ripples across multiple layers. As the platform grows, layered architecture leads to:

- Large, unfocused service classes that touch multiple features
- Merge conflicts when multiple features modify the same layer
- Difficulty understanding a feature end-to-end without jumping across folders
- Tight coupling between unrelated features through shared service layers

## Decision

Organize code by **feature slice**, not by technical layer. Each feature is a self-contained vertical slice that owns its entire stack:

```
Features/
  Playground/
    Api/          — Endpoints, request/response contracts
    Application/  — Use cases, DTOs, validators
    Domain/       — Entities, value objects, enums
    Infrastructure/ — Data access, external service calls
    PlaygroundModule.cs — DI registration for this slice
```

Within each slice, code follows **Clean Architecture** conventions (Domain -> Application -> Infrastructure -> Api), but the slice boundary is the primary organizational unit.

Cross-cutting concerns (Result pattern, provider interfaces, middleware) live in `Common/`.

## Consequences

### Positive

- Each feature is self-contained — easy to understand, modify, and test in isolation
- New features are added by creating a new folder, not by modifying existing layers
- Merge conflicts are rare since features don't share code paths
- Features can be developed in parallel by different contributors
- Delete a feature by deleting its folder

### Negative

- Some code duplication across slices (e.g., similar mapping logic) — accepted as a trade-off for independence
- Developers unfamiliar with the pattern may initially look for a "Services" folder
- Cross-feature queries (e.g., analytics aggregating across Experiments and Playground) require explicit coordination

### Neutral

- Each feature module registers its own services via `IServiceCollection` extensions
- Shared abstractions (interfaces, base types) live in `Common/` and are referenced by slices

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Traditional layered (Controllers/Services/Repos) | Familiar pattern, easy to start | Coupling increases over time, hard to maintain at scale | 14+ features would create bloated service layers |
| Microservices | Maximum isolation | Massive operational overhead for a local-first research tool | Over-engineered for single-user desktop deployment |
| Modular monolith (feature modules, no Clean Arch within) | Simpler than Clean Arch per slice | Slices become messy as complexity grows | Lose the dependency direction guarantees within each slice |

## References

- Jimmy Bogard — [Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/)
- See `ARCHITECTURE.md` for full project structure

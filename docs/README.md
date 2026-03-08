# AI Research Workbench — Documentation

## Project Documentation

| Document | Description |
|----------|-------------|
| [DESIGN.md](/DESIGN.md) | Vision, features, wireframes, data models, API surface, deployment |
| [ARCHITECTURE.md](/ARCHITECTURE.md) | Vertical slice structure, Result pattern, provider abstractions, middleware, testing |
| [PROJECT_PLAN.md](/PROJECT_PLAN.md) | Phased task breakdown (~150 tasks across 5 phases) |
| [AGENTS.md](/AGENTS.md) | Claude Code agent modes (Feature Builder, Debugger, etc.) |
| [SKILLS.md](/SKILLS.md) | Claude Code skills (create feature, add endpoint, write handler, etc.) |

### Implementation Plans

| Document | Description |
|----------|-------------|
| [Phase 1 Kickoff](./plans/PHASE1_KICKOFF.md) | Phase 1 (Walk) kickoff prompt — Playground, Token Explorer, Models, History |
| [Phase 2 Plan](./plans/PHASE2_PLAN.md) | Phase 2 (Jog) detailed implementation plan — domain models, endpoints, execution order |
| [Phase 2 Kickoff](./plans/PHASE2_KICKOFF.md) | Phase 2 (Jog) kickoff prompt — Prompt Lab, Experiments, Multi-Pane Playground |

### Feature Documentation

| Document | Description |
|----------|-------------|
| [Playground](./features/playground.md) | Chat interface, SSE streaming, logprobs visualization, conversation management, export |
| [Token Explorer](./features/token-explorer.md) | Next-token prediction, step-through, branching, tokenization, cross-model comparison |

### Platform Design Documents

| Document | Description |
|----------|-------------|
| [PLATFORM_AGENTS.md](./PLATFORM_AGENTS.md) | Application agent architecture: 6 platform agents, user-built agents, execution engine, patterns |
| [PLATFORM_SKILLS.md](./PLATFORM_SKILLS.md) | Application skill registry: 30+ atomic skills, ISkill interface, skill compositions |

## Architecture Decision Records

All significant architectural decisions are recorded in [`/docs/ADR/`](./ADR/).

| ADR | Title | Status |
|-----|-------|--------|
| [ADR-001](./ADR/001-vertical-slice-architecture.md) | Vertical Slice Architecture | Accepted |
| [ADR-002](./ADR/002-result-pattern.md) | Result Pattern for Error Handling | Accepted |
| [ADR-003](./ADR/003-cache-abstraction.md) | Cache Provider Abstraction | Accepted |
| [ADR-004](./ADR/004-file-storage-abstraction.md) | File Storage Abstraction | Accepted |
| [ADR-005](./ADR/005-auth-abstraction.md) | Authentication Provider Abstraction | Accepted |
| [ADR-006](./ADR/006-inference-provider-abstraction.md) | Inference Provider Abstraction | Accepted |
| [ADR-007](./ADR/007-dotnet9-minimal-api.md) | .NET 9 Minimal API over Controllers | Accepted |
| [ADR-008](./ADR/008-database-abstraction.md) | Database Abstraction via EF Core | Accepted |
| [ADR-009](./ADR/009-vector-store-abstraction.md) | Vector Store Abstraction for RAG | Accepted |
| [ADR-010](./ADR/010-project-naming.md) | Project Name — Prism | Accepted |
| [ADR-011](./ADR/011-contextual-help-panels.md) | Contextual Help Panels on Feature Views | Accepted |
| [ADR-012](./ADR/012-parameter-tooltips.md) | Parameter Tooltips on All Configuration Controls | Accepted |

## Conventions

- ADRs follow the template in [`/docs/ADR/template.md`](./ADR/template.md)
- ADRs are numbered sequentially and never deleted — superseded ADRs are marked as such
- All public interfaces and types require XML documentation (compiler-enforced)
- Structured logging with correlation IDs on every request

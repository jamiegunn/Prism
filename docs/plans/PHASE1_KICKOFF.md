# Phase 1 Kickoff Prompt

Use this prompt to start building **Prism** — the AI Research Workbench. Copy everything below the line and paste it into a new Claude Code session in the `C:\dev\AI_Research` directory.

---

## Prompt

You are building **Prism**, an all-in-one AI research platform. The project name is **Prism** (see ADR-010). All code namespaces use `Prism.*` (e.g., `Prism.Api`, `Prism.Common`, `Prism.Features`). The database is named `prism`. The window title is "Prism — AI Research Workbench". All design docs are complete. Your job is to implement Phase 1 (Walk) from scratch.

**Read these files first** (in this order — do not skip any):
1. `CLAUDE.md` — your instructions for how to work on this project
2. `AGENTS.md` — agent modes you should adopt based on the task
3. `SKILLS.md` — step-by-step procedures for common tasks
4. `ARCHITECTURE.md` — the full architecture (structure, patterns, abstractions, interfaces)
5. `PROJECT_PLAN.md` — Phase 1 task breakdown (tasks 1.1.x through 1.5.x)
6. `DESIGN.md` — full design document (data models, API surface, wireframes)

After reading all docs, execute Phase 1 in the following order. Do not skip ahead. Complete each section before moving to the next. Ask me before generating any database migration.

---

### Step 1: Project Scaffolding (Tasks 1.1.1–1.1.13)

Create the solution structure following the vertical slice architecture defined in `ARCHITECTURE.md`:

**Backend:**
- .NET 9 solution with projects: `Prism.Api`, `Prism.Common`, `Prism.Features`, `Prism.Tests`
- Use the project names and structure from ARCHITECTURE.md, NOT the ones in PROJECT_PLAN.md (the plan was written before the architecture was finalized — ARCHITECTURE.md is the source of truth)
- `Prism.Api`: Program.cs with Minimal API, middleware pipeline (CorrelationId → GlobalException → RequestLogging → CORS → Endpoints), Aspire ServiceDefaults for OpenTelemetry
- `Prism.Common`: Result<T> pattern (full implementation with Match/Map/Bind), Error types, BaseEntity, AppDbContext, all provider interfaces (IInferenceProvider, ICacheService, IFileStorage, IAuthProvider, IVectorStore, IGlobalSearch), EnvironmentSnapshot, provider implementations for Phase 1 (VllmProvider, InMemoryCacheService, NullCacheService, LocalFileStorage, NoAuthProvider)
- `Prism.Features`: Empty feature folders (Playground/, TokenExplorer/, History/, Models/) — we'll fill these in subsequent steps
- Configure: Serilog structured logging, EF Core with Npgsql, FluentValidation, appsettings.json with provider config sections (Database, Cache, Storage, Auth, InferenceProviders)
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<DocumentationFile>` in all .csproj files to enforce XML docs
- RecordingInferenceProvider decorator and RateLimitedInferenceProvider decorator wired in DI

**Frontend:**
- Vite + React + TypeScript in `frontend/`
- Tailwind CSS + shadcn/ui (init + base components: Button, Input, Card, Tabs, Select, Slider, ScrollArea, Sheet, Dialog, Tooltip, Separator, Badge, DropdownMenu)
- TanStack Query + Zustand + React Router + React Hook Form + Zod
- App shell with sidebar (all 14 modules listed, only Playground/Token Explorer/Models active, rest show "Coming in Phase N")
- Status bar (bottom): provider status, GPU %, active model
- Dark mode default
- `orval.config.ts` for generated API client (configure but don't generate yet — backend API needs to exist first)

**Infrastructure:**
- `docker-compose.yml`: PostgreSQL 16 with pgvector extension, port 5432, volume for persistence
- `docker-compose.dev.yml`: adds vLLM service (configurable model via env var, GPU passthrough)
- `.env.example` with all required environment variables
- `appsettings.Development.json` with local defaults

Pause here and show me the full file tree of what you created. Do not proceed until I confirm.

---

### Step 2: Model Management Feature (Tasks 1.2.1–1.2.11)

Build the Models feature slice using the Feature Builder agent mode:

**Backend — `Features/Models/`:**
- Domain: `InferenceInstance` entity (Id, Name, Endpoint, ProviderType, Status enum, ModelId, GpuConfig, MaxContextLength, Capabilities as ProviderCapabilities, CreatedAt, LastHealthCheck)
- Application: RegisterInstance, UnregisterInstance, GetInstanceMetrics, ListInstances, SwapModel, SwapProvider handlers — all returning Result<T>
- Infrastructure: `InferenceInstanceConfiguration` (table: `models_instances`), `HealthCheckBackgroundService` (polls every 30s), `ProviderConfigWatcher`
- Api: `ModelEndpoints.cs` with route group `/api/v1/models` — CRUD + metrics + swap + reload-config endpoints
- Module: `ModelsModule.cs` with `AddModelsFeature()` DI registration

**Frontend — `features/models/`:**
- ModelsPage.tsx: list registered instances with status indicators (green/yellow/red dot), model name, endpoint, GPU %, KV cache %
- RegisterInstanceDialog: enter endpoint URL, select provider type, auto-detect model on submit
- InstanceDetailPanel: full model info, real-time metrics (auto-refresh 5s), supported capabilities badges
- KV cache gauge visualization (green < 70%, yellow 70-90%, red > 90%)

Generate the EF migration for InferenceInstance after I approve the entity design.

---

### Step 3: Inference Playground + Logprobs (Tasks 1.3.1–1.3.22)

Build the Playground feature slice — this is the core of the platform:

**Backend — `Features/Playground/`:**
- Domain: `Conversation` (aggregate root), `Message` (entity with LogprobsData JSON, Perplexity), `ConversationParameters` (value object)
- Application: SendMessage (streaming via SSE), GetConversation, ListConversations, DeleteConversation, ExportConversation handlers
- Application: LogprobsCalculator integration — compute perplexity, entropy, surprise flags from raw logprobs
- Infrastructure: `ConversationConfiguration` (table: `playground_conversations`), `MessageConfiguration` (table: `playground_messages`)
- Api: SSE streaming endpoint (`POST /api/v1/inference/chat` with `text/event-stream`), conversation CRUD endpoints
- The SSE endpoint must proxy to vLLM, forward chunks to the client, collect timing (TTFT, total latency, tok/s), and feed the RecordingInferenceProvider

**Frontend — `features/playground/`:**
- PlaygroundPage with parameter sidebar (model selector, temperature slider 0–2, top_p, top_k, max_tokens, stop sequences, frequency/presence penalty, logprobs toggle + top_logprobs selector 1–20)
- ChatPane: message bubbles with markdown rendering, auto-scroll, streaming token-by-token append, Stop button, Shift+Enter for newline
- SystemPromptEditor: collapsible panel, save to library, quick-swap dropdown
- SSE streaming hook (`useStreamChat.ts`)
- Token usage display: prompt/completion/total tokens, latency, TTFT, tok/s, estimated cost
- **Logprobs visualizations** (the research-critical UI):
  - TokenHeatmap: each token colored by logprob (green ≥ -0.1 → yellow -1.0 → red < -3.0), tooltip with exact probability/logprob/rank
  - AlternativeTokensPanel: click any token → bar chart of top-K alternatives with probabilities, entropy at position, greedy vs sampled indicator
  - PerplexityBadge: color-coded (green < 3, yellow 3–6, red > 6)
  - EntropyChart: small bar per token, height ∝ entropy
  - SurpriseHighlighting: underline tokens below probability threshold (configurable, default 10%)
- ConversationHistory sidebar: list saved conversations, search, click to reload, delete
- Export conversation: JSON (with logprobs), Markdown, JSONL

---

### Step 4: Next-Token Prediction Explorer (Tasks 1.4.1–1.4.9)

Build the TokenExplorer feature slice:

**Backend — `Features/TokenExplorer/`:**
- Application: PredictNextToken (calls vLLM with max_tokens=1, logprobs=true, top_logprobs=20), StepThroughGeneration (append selected token, predict next), ExploreBranch (force a token, generate N more)
- Api: `POST /api/v1/inference/next-token`, `POST /api/v1/inference/step`, `POST /api/v1/inference/branch`

**Frontend — `features/token-explorer/`:**
- TokenExplorerPage: prompt input area, ranked list of top-20 predicted next tokens with probability bars
- ProbabilityDistribution: horizontal bar chart with cumulative probability line, top-p and top-k cutoff indicators
- StepThroughView: "Step" button (model picks), "Force" (click any token to select it), growing output with tokens colored by probability, Undo button
- BranchTreeView: visual tree of explored branches, highlight greedy path vs sampled path
- SamplingVisualization: show effective vocabulary size, temperature effect, how top-p/top-k change the distribution

---

### Step 5: Tokenizer Explorer (Tasks 1.5.1–1.5.7)

This can be a lightweight addition — add to the TokenExplorer feature or create a small separate slice:

**Backend:**
- Endpoints: `GET /api/v1/models/instances/{id}/tokenizer` (tokenizer info), `POST /api/v1/inference/tokenize` (text → tokens), `POST /api/v1/inference/detokenize` (token IDs → text)

**Frontend — `features/token-explorer/` (new tab):**
- Text input area, tokenized output as colored blocks (alternating colors), token ID + byte length per block
- Hover: tooltip with token ID, hex bytes, Unicode codepoints
- Multi-model comparison: side-by-side tokenization of same text
- Token cost estimator

---

### Step 6: History & Replay (wiring, not full feature)

The RecordingInferenceProvider decorator is already capturing history from Step 1. Now expose it:

**Backend — `Features/History/`:**
- Domain: `InferenceRecord`, `ReplaySession`, `ReplayResult` (from ARCHITECTURE.md)
- Application: SearchHistory, GetRecord, TagRecord, ReplaySingle handlers
- Api: History browse/search/tag endpoints, single replay endpoint
- Full batch/group replay is Phase 2 — just wire up single replay and browsing for now

**Frontend — `features/history/`:**
- HistoryPage: paginated table of all inference records (timestamp, model, source module, prompt preview, tokens, latency)
- Filters: by model, module, date range, tags
- Click record → detail view with full request/response + logprobs
- Tag management: add/remove tags on records
- "Replay" button → re-execute against current model, show side-by-side diff

---

### Step 7: Seed Data & Use Cases

- Run `IDataSeeder` on first launch to populate:
  - 3 sample system prompts (general assistant, code helper, JSON extractor)
  - 2 built-in use cases with step-by-step instructions (Model Comparison, Hallucination Detection)
  - Sample conversation demonstrating logprobs features

---

### Step 8: Final Wiring & Polish

- Generate the OpenAPI spec from the backend
- Run `npm run api:generate` to create the typed TypeScript client via orval
- Replace any hand-written API calls with generated hooks
- Verify the full flow: register vLLM instance → chat in Playground with logprobs → explore tokens in Token Explorer → browse history → replay
- Run all tests
- Update `docs/README.md` if any new ADRs were created during implementation

---

## Important Reminders

- **ARCHITECTURE.md is the source of truth** for project structure, naming, and patterns. Where PROJECT_PLAN.md conflicts (e.g., project names like "Core" vs "Common"), follow ARCHITECTURE.md.
- **Every public type/method needs XML doc comments.** The compiler will enforce this.
- **All handlers return Result<T>.** No exceptions for expected failures.
- **No raw SQL, no System.IO.File, no IMemoryCache in feature code.** Use the abstractions.
- **Feature-prefixed tables:** `playground_conversations`, `models_instances`, `history_records`, etc.
- **Ask before generating migrations.** Show me the entity design first.
- **Dark mode default.** All UI components must support both themes.
- **Structured logging with named properties.** Never string interpolation in log calls.

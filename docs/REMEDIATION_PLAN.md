# Prism — Remediation Plan

**Goal:** every claim in the README is true, provable by an automated gate, and a junior AI
researcher can go from `git clone` to a defensible, reproducible research artifact without
being told how by a human.

**Decisions this plan encodes** (from the scoping conversation):

| Decision | Choice |
|---|---|
| Scope | Build every module to the claim — no cutting, no "preview" labels |
| Coverage | 90% line / 80% branch of *logic*, boilerplate excluded from the denominator |
| Verify loop | Runs on your machine (see §0.1 — this required correcting) |
| Onboarding | Guided research recipes that terminate in a reproducible export |

---

## 0. Ground truth before anything else

### 0.1 The verification environment — tested, not assumed

You chose "run the loop on my machine." I tested that before planning around it, and it does
not work the way it sounds. Evidence:

| Environment | dotnet | docker | network | verdict |
|---|---|---|---|---|
| Device bridge VM (`device_bash`, the Linux VM on your laptop) | absent | absent | blocked (403 proxy) | **cannot run backend** |
| Cloud sandbox (this session) | absent; `dot.net`, `builds.dotnet.microsoft.com`, `aka.ms`, `packages.microsoft.com`, `api.nuget.org`, `mcr.microsoft.com` all return 000/403 | binary present, daemon absent | npm registry only | **cannot run backend** |
| Cloud sandbox — frontend | node 22.22.2, npm reachable, Playwright browsers preinstalled at `/opt/pw-browsers` | — | ok | **can fully run frontend** |

Ubuntu's archive offers .NET SDK **8.0** only; the solution targets `net9.0`. `apt` cannot
close the gap and the Microsoft feeds are unreachable from both environments.

**Consequence — this is the plan's single biggest logistical constraint:**

- **Backend** (`dotnet build/test/format`, EF migrations, Testcontainers, Stryker) must execute
  on your macOS host directly — your terminal, or a Cowork task running *on your computer*
  rather than in the cloud, or GitHub Actions.
- **Frontend** (Vitest, ESLint, `tsc -b`, Vite build, Playwright, Storybook) can be iterated
  end-to-end in the cloud sandbox against a mocked API.
- **Integration and E2E against a live backend** need Docker + .NET 9 together, so they belong
  on your host or in CI.

Every gate in this plan carries a **Runs:** tag — `host`, `cloud`, or `CI` — so no phase is
ever blocked on discovering this again mid-flight.

**Assumption A0, unverified and blocking:** your macOS host has .NET 9 SDK and Docker Desktop
installed and working. Prove it first, before Phase 1:

```bash
dotnet --list-sdks              # expect a 9.0.x line
docker info                     # expect a running daemon
cd backend && dotnet build      # expect 0 errors  <-- nobody has confirmed this compiles
docker compose up -d && docker compose ps
```

If `dotnet build` does not currently succeed, everything downstream re-sequences, because
right now **no evidence exists that the backend compiles at all** — CI has never run green on
a commit that mattered, and the audit was static.

### 0.2 The assumption ledger

Create `docs/assumptions.md`. Every belief that work depends on gets a row: `ID | claim |
status (unproven/proven/falsified) | proof method | evidence link`. Nothing is marked
"proven" by reading code — only by a command whose output is pasted in, or a test that fails
when the claim is false.

Seeded from the audit, all currently **unproven**:

| ID | Assumption | Proof |
|---|---|---|
| A0 | The backend compiles on .NET 9 | `dotnet build` exit 0 |
| A1 | The 57 existing tests pass | `dotnet test` exit 0 |
| A2 | `dotnet format --verify-no-changes` passes | run it |
| A3 | Migrations apply cleanly to an empty Postgres 16 + pgvector | `dotnet ef database update` on a fresh container |
| A4 | The app boots and serves `/health` | `curl localhost:5000/health` |
| A5 | `.Include("")` throws at runtime (audit's read of EF Core) | integration test hitting `POST /rag/collections/{id}/query` |
| A6 | The 10s `HttpClient.Timeout` kills real generations | test with a delayed stub provider |
| A7 | The unassigned `streamError` loses the assistant message | SSE test that faults mid-stream |
| A8 | `UsageLog` / `EvaluationResult` / `BatchResult` are never written | schema-level test: exercise every endpoint, assert row counts stay 0 (should fail *after* the fix) |
| A9 | The JupyterLite iframe 404s | Playwright network assertion |
| A10 | Prompt Lab version-diff 404s | integration test on `/prompts/{id}/versions/diff` |
| A11 | Ollama truly cannot return logprobs (README says it can, code says it can't) | contract test against a live `ollama serve` |
| A12 | vLLM guided decoding needs `guided_json`, not bare `response_format` | contract test against live vLLM |
| A13 | Storybook/Playwright CI jobs are red today | push a no-op branch, read the run |
| A14 | The `dev.sh` / `dev.ps1` quick start works from a clean clone | fresh clone in a scratch dir, run it, time it |
| A15 | pgvector similarity is unindexed (no HNSW) | `EXPLAIN ANALYZE` the query at 100k chunks |

A15 and A14 are the kind nobody checks and everybody pays for.

---

## 1. Phase map

Ten phases. Each has a **gate** — a command that must pass — and each gate lands in CI the
moment it exists, so no phase can silently regress a previous one.

```
P0  Harness & truth      →  P1  Kill the three bugs   →  P2  Recording spine
                                                              ↓
P3  Durable job runner   →  P4  Module completion (×8) →  P5  Provider correctness
                                                              ↓
P6  Test suite to 90%    →  P7  UX & onboarding       →  P8  Reproducible export
                                                              ↓
                                          P9  Ten falsification cycles
```

P6 is drawn as one block but is really continuous: **every phase from P1 onward ships its
tests in the same commit as its code.** Retrofitting 1,500 tests at the end is how test
suites become theater.

---

## P0 — Harness and truth reconciliation

*Runs: host + CI. ~1 week.*

Nothing is fixable until the scoreboard is honest.

**0.1 Pin the toolchain.** Add `global.json` pinning the .NET 9 SDK feature band. Add
`.nvmrc` (node 22). Add `Directory.Build.props` with `TreatWarningsAsErrors`, `Nullable`,
and a shared `LangVersion`. Currently a contributor's SDK choice is luck.

**0.2 Make `Program` testable.** Append `public partial class Program { }` to `Program.cs`
so `WebApplicationFactory<Program>` works. Without this, endpoint-level integration tests
cannot be written at all — this one line unblocks ~3,300 lines of endpoint coverage.

**0.3 Coverage instrumentation with an honest denominator.** Add `coverlet.collector` plus a
`.runsettings` that excludes, by attribute and by path:

- migrations (`**/Migrations/**`) — **25,639 lines, 43% of the backend**
- DTOs/records (1,668), EF `IEntityTypeConfiguration` (1,568), seeders (1,601)
- `Program.cs`, `GlobalUsings.cs`, `Marker.cs`

That leaves a real denominator of **≈28,000 lines**, of which the meaningful core is
handlers 10,240 · endpoints 3,303 · inference providers 3,854 · domain 3,491 · DI 1,373.
Reporting "90%" against the raw 59,841 would be a lie of the same species as the current
README, so the exclusion list itself goes in the repo and in the README.

**0.4 The anti-gaming gate: mutation testing.** 90% line coverage is trivially achievable
with tests that assert nothing. Add **Stryker.NET** with a mutation-score floor (start 50%,
ratchet to 70%). A test suite that executes code without asserting on it dies here. This is
the difference between "90% coverage" and "quality unit tests," and it is the single most
important item in P0.

**0.5 Coverage ratchet, not a cliff.** CI fails if coverage drops below `max(current, target)`
recorded in a checked-in baseline file. Going from 2% to 90% in one commit is impossible;
never going backwards is enforceable from day one.

**0.6 Repair CI's escape hatch.** In `.github/workflows/ci.yml`:
- `ci-summary` currently `exit 1`s on only 4 of 8 upstream jobs. Make it gate **all** of them.
- Actually install `@playwright/test` and the Storybook framework packages — both jobs are
  guaranteed-red today and deliberately un-gated.
- Implement `--export-openapi` in the API so `openapi-drift` stops being a permanent no-op;
  commit `frontend/openapi.json`; make drift a hard failure.
- Run `npm run api:generate` for real, commit `src/services/generated/`, and make `orval-drift`
  meaningful. (Decide deliberately: adopt the generated client and delete the ~15 hand-written
  `api.ts` files, or delete the orval config and stop claiming a generated client. Half-and-half
  is what produced the Prompt Lab 404.)
- Add jobs: coverage gate, mutation gate, `tsc` over **all** files (see 0.7), a11y, migration
  round-trip.

**0.7 Un-hide the broken files.** `tsconfig.app.json` excludes `src/test/playwright` and
`**/*.stories.tsx` — precisely the files that would not compile. `playwright.config.ts` and
`.storybook/*` are in no project at all. Remove the exclusions, add the configs to a tsconfig
project, install the missing packages, and let the typecheck tell the truth.

**0.8 Make `product-truth.yaml` executable.** Today it is an honest document the README
contradicts. Convert it into the source of truth and add a test that fails when README and
YAML disagree — parse both, compare module status and the test/CI counts the README cites.
A doc that can lie will lie again in six commits; this is exactly how the current gap opened.

**Gate P0** — *Runs: CI*
```
dotnet build -warnaserror && dotnet test && dotnet format --verify-no-changes
&& coverage-report generated && stryker baseline recorded
&& npx tsc -b (no exclusions) && npm run lint && npm run build
&& storybook build && playwright test   # both actually execute
&& node scripts/check-doc-truth.mjs
```
All 9+ jobs enforced by `ci-summary`. **No further phase starts until this is green.**

---

## P1 — The three runtime bugs

*Runs: host. ~2 days. Regression test first, in the same commit.*

Each fix is written test-first: the test must **fail on the current code** — that is the
proof the bug is real (A5/A6/A7), and the audit's word is not accepted as evidence.

| Bug | Location | Fix | Failing test to write first |
|---|---|---|---|
| `.Include("")` breaks all vector + hybrid RAG search | `Rag/Application/QueryCollection/QueryCollectionHandler.cs:85` | delete the call | integration: seed a collection, `POST /query`, assert 200 + ranked hits |
| 10s timeout kills every generation, sweep, and agent step | `Models/Application/InferenceProviderFactory.cs:37` | per-operation timeouts (health 5s, tokenize 10s, chat 300s, pull 1800s) via named `HttpClient` + Polly, all configurable | stub provider that delays 15s; assert completion, then assert a 301s delay *does* cancel cleanly |
| `streamError` declared but never assigned → mid-stream fault aborts SSE and loses the assistant message | `Playground/Application/StreamChat/StreamChatHandler.cs:213,266` | assign in `catch`, emit an `error` SSE frame, persist the partial message | fault injection mid-`await foreach`; assert an `error` frame arrives *and* the partial row is persisted |

Add a Roslyn analyzer rule or `-warnaserror` on CS0219 (assigned-but-unused / never-assigned)
so the `streamError` class of bug cannot recur silently.

**Gate P1:** three previously-failing tests now pass; P0 gate still green.

---

## P2 — The recording spine

*Runs: host. ~1.5 weeks. This is the highest-leverage change in the plan.*

"Every call is recorded" is false: only `ReplayService` uses `IInferenceRuntime`. Playground,
Token Explorer, RAG, Agents, Experiments and Prompt Lab all bypass it by calling
`InferenceProviderFactory` directly. Consequently `UsageLog` has zero writers and Analytics
aggregates an empty table forever, while `HistorySeeder` fakes rows (`Perplexity = 2.34`) that
make the History page look alive.

**Fix:** make the runtime the *only* path to a provider.

1. Every feature takes `IInferenceRuntime`, never `IInferenceProviderFactory`.
2. Enforce architecturally, not by discipline — a **NetArchTest/ArchUnitNET test** that fails
   the build if any type outside `Prism.Common.Inference` references `IInferenceProviderFactory`.
   Same test class also enforces the vertical-slice rules in `CLAUDE.md` (no cross-slice
   references, no EF types in `Api/`), which are currently honor-system.
3. `ChannelInferenceRecorder` writes `InferenceRun` + `TokenEvent` + **`UsageLog`** on every
   call, including failures (status, error class, retry count).
4. Cost: wire the existing, unused `CostCalculator` into the recorder; add per-model pricing
   config with an explicit `null` (not `0`) for local models so "free" and "unknown" are
   distinguishable in Analytics.
5. Seeders: add `IsSample` to every seeded entity, badge it in the UI, and add a
   `--no-seed` flag. Fabricated demo data that is indistinguishable from real results is the
   most dangerous thing in this repo for a junior researcher.

**This one phase makes History, Analytics, and cost real simultaneously** — three "hollow"
modules fixed by one correctly-placed abstraction.

**Gate P2** — *Runs: CI (integration)*: exercise one call through each of the 7 features;
assert `InferenceRun`, `TokenEvent` and `UsageLog` rows appear for all 7; assert the arch test
forbids direct factory use; assert Analytics returns non-zero given real traffic (A8 inverted).

---

## P3 — The durable job runner

*Runs: host. ~2 weeks. Unblocks four modules at once.*

ADR-016 is accepted but unimplemented. `IJobQueue`/`IJobStore`/`DurableJob` exist with zero
consumers. Evaluation, Batch, Fine-Tuning and RAG ingestion all insert `Pending`/`Queued` rows
that nothing ever dequeues.

Build **one** runner, correctly, and let four features share it:

- `IHostedService` worker pool, configurable concurrency
- Postgres-backed queue with `FOR UPDATE SKIP LOCKED` — lease + heartbeat + lease expiry, so a
  crashed worker's job is reclaimed rather than lost or double-run
- idempotency keys; at-least-once delivery with idempotent handlers
- retry with exponential backoff + jitter, poison-queue after N attempts, dead-letter visible in UI
- cancellation that actually propagates to the in-flight HTTP call
- progress reporting (`n/total`, ETA) streamed to the UI over SSE
- structured events → OpenTelemetry spans, so a stuck job is diagnosable

**Test it like infrastructure, not like a feature.** This is where "test all assumptions"
earns its keep: kill a worker mid-job and assert reclaim; run two workers and assert no
double-execution; expire a lease and assert exactly-once side effects; enqueue 10k jobs and
assert no lost work; restart the process mid-queue and assert resumption. Property-based
(FsCheck) over the state machine: no sequence of crash/retry/cancel events reaches an invalid
state. Target **95%** coverage here, above the global bar — everything else rests on it.

**Gate P3:** chaos suite green; a job survives `docker restart` of the API container with
exactly-once semantics.

---

## P4 — Module completion

*Runs: host. ~7 weeks. Each module ships code + tests + UI + docs together.*

Sequenced so each depends only on what precedes it.

**P4.1 Evaluation (~1.5 wk).** The scorers are real and correctly implemented — LCS-DP
ROUGE-L, clipped n-gram BLEU with brevity penalty — and are registered in DI and *never
called by anything*. Wire them: a job-runner-backed evaluation pipeline that iterates dataset
records, calls the runtime, scores, and **writes `EvaluationResult` rows** (currently written
by zero lines of code, which is why `/results`, `/leaderboard` and `/export` are permanently
empty). Register `LlmJudgeScorer` (unregistered today) and fix its `Model = ""` bug, which
every provider rejects. Add the two scorers the README claims and that do not exist:
**perplexity** and **calibration** (ECE + reliability curve). Golden-file tests against
published ROUGE/BLEU reference implementations — the math is right today, and a test suite
that lets someone "simplify" it later is worthless.

**P4.2 Batch Inference (~1 wk).** Consume the job runner. Write `BatchResult` rows. Fix
`UpdateBatchJobStatusHandler:45`, where pause requires a `Running` state nothing can currently
produce, so pause always 400s. Replace `EstimateBatchCostHandler:44`'s hardcoded
`recordCount * 500` tokens (which ignores the model and returns no cost field despite the
endpoint's name) with real tokenizer-based estimation and `CostCalculator`. Add per-record
retry, partial-result streaming, and the CSV/JSONL download the endpoint already exposes but
no UI reaches.

**P4.3 Analytics (~0.5 wk).** Mostly free after P2. Add the missing views: cost-by-project,
latency histogram, provider reliability. Fix the two handlers that `ToListAsync()` an entire
unbounded table and aggregate in memory — push to SQL, add covering indexes, and add a
performance test at 1M `UsageLog` rows (p95 < 500ms).

**P4.4 Structured Output (~0.5 wk).** `StructuredInferenceHandler` passes a bare JSON Schema
as `ResponseFormat`, and `OpenAiCompatibleProvider:412` emits it as `response_format: <raw
schema>`. vLLM wants `guided_json`; OpenAI wants `{"type":"json_schema","json_schema":{…}}`.
Neither gets what it expects, so guided decoding does not work anywhere. Emit per-provider
correct payloads; add capability-gated fallback (constrained retry + validation) for providers
without native guidance; replace the hand-rolled validator that `catch {}`-swallows schema
parse failures with a real JSON Schema library; persist validation failures for the failure
inspector the README promises.

**P4.5 RAG (~1 wk).** `RagChunkConfiguration:11` claims an HNSW index; the migration only
creates a GIN index on `search_vector`, and the `vector` column has no dimension — so
similarity search is an unindexed full scan (A15). Add a dimensioned column + HNSW index +
migration. Make the embedding endpoint **per-instance** rather than a single global
`Embedding:BaseUrl` that silently defaults to `http://localhost:8000` — an Ollama-only user
currently gets no embeddings and no error explaining why. Note honestly in the docs that the
"BM25" path is Postgres `ts_rank` FTS, not BM25 — either implement BM25 or rename it. Add the
chunking preview, retrieval debugger, and citation mapping.

**P4.6 Fine-Tuning (~1 wk).** `LoraAdapter`/`AdapterPath`/`IsActive` appear nowhere outside
the slice and its migrations — adapters never reach an inference call. Wire vLLM LoRA
load/unload, real external job launch adapters (start with one: vLLM/PEFT via the job runner),
status polling, model registration on completion, and pre/post evaluation comparison. The
dataset export (232 real lines, Alpaca/ShareGPT) is the one solid piece — keep it and test it.

**P4.7 Notebooks (~1 wk).** `frontend/public/jupyterlite/` holds two files, 16KB, no built
assets; the iframe points at `/jupyterlite/lab/index.html` while `setup.sh` outputs to
`/jupyterlite/output/lab/index.html`, so the paths would not match even after someone runs it.
In dev, Vite's SPA fallback serves Prism *into its own iframe*. Build JupyterLite as a real
CI artifact, fix the path, and ship the promised Python SDK (`prism.runs()`, `prism.prompts()`,
`prism.datasets()`) as a tested package, not a 251-line `workbench.py` helper.

**P4.8 History & Prompt Lab (~0.5 wk).** Standardized `InferenceRun`/`InferenceTrace`/
`TokenEvent` schemas; replay produces a linked `ReplayRun`; side-by-side and trace-level diff.
Fix the Prompt Lab route mismatch: frontend calls `/prompts/{id}/versions/diff`, backend
exposes `/prompts/{id}/diff`, and the sibling `/versions/{version:int}` route's `:int`
constraint means `"diff"` matches nothing — the version diff viewer is dead on arrival (A10).
The OpenAPI drift gate from P0.6 is what prevents the next one of these.

Also connect the 7 orphaned backend endpoints with no frontend consumer (batch download, eval
export, tokenizer info, annotate, workspace detail, …) or delete them.

---

## P5 — Provider correctness

*Runs: host, with live vLLM + Ollama + LM Studio. ~1 week.*

The three providers are genuinely distinct and honest in code — Ollama speaks native
`/api/chat` NDJSON, declares `SupportsLogprobs=false`, and returns `LogprobsData = null`. The
**README is what lies**: "Supports logprobs and streaming" for Ollama, and a capability table
claiming Ollama logprobs up to 5.

1. Fix the README table to match the code (or implement it, if Ollama's API has since gained
   the capability — verify against a live instance, don't assume either direction: **A11**).
2. LM Studio is aliased to `OpenAiCompatibleProvider` (`InferenceProviderFactory:47`), so its
   real JIT load/unload is reported unsupported. Either implement a genuine LM Studio provider
   or document the alias explicitly.
3. `ProviderCapabilityRegistry:101-183` probes only two things — `TokenizeAsync("Hello world")`
   and `GetMetricsAsync()`. Logprobs, streaming, guided decoding and hot-reload are copied from
   compile-time constants; function-calling and multimodal are hardcoded `false`. Probe all of
   them live, with a 1-token request each.
4. Fix `ProviderCapabilityRegistry:181`: `ProbeSucceeded = healthOk || probeError is null`
   records a cleanly-unhealthy endpoint as a successful probe.
5. **Contract test suite** — one shared xUnit theory run against every provider, verifying the
   declared capability matrix matches observed behavior. Two tiers: recorded-cassette tests in
   CI (WireMock.NET), live-provider tests behind `[Trait("Category","Live")]` run nightly on
   your host. This is how the capability table stops drifting from reality permanently.
6. Add the missing local tokenizer fallback so tokenization works without vLLM.

---

## P6 — The test suite

*Runs: cloud (frontend) + host/CI (backend). ~7 weeks, interleaved with P1–P5.*

### What "quality" means here

Coverage is the floor, not the goal. Every test must satisfy: **it fails when the behavior
breaks, and it names the behavior in its title.** Enforced by Stryker (P0.4) — a mutant that
survives means a test executed code without asserting on its meaning.

Per-layer strategy:

| Layer | Lines | Approach | Est. tests |
|---|---:|---|---:|
| Handlers (118 files) | 10,240 | happy path · every validation branch · every `Result` error variant · cancellation · concurrency | 700–950 |
| Endpoints (61 files, 123 routes) | 3,303 | `WebApplicationFactory` integration: status codes, auth, model binding, content negotiation, problem-details shape | 250–350 |
| Inference providers | 3,854 | WireMock cassettes + live contract theory (P5.5) | 150–200 |
| Domain (72 entities + scorers + chunkers) | 3,491 | golden files for scorers; property-based (FsCheck) for chunkers and logprob math | 200–300 |
| Job runner | — | chaos + property-based state machine (P3) | 80–120 |
| DI/composition | 1,373 | container-validation test: every registered service resolves | 20–30 |
| **Backend total** | **≈28,000** | | **1,400–1,950** |
| Frontend | 19,652 | Vitest + RTL + MSW; hooks, stores, logprob math, error/empty/loading states | 400–600 |
| E2E | — | Playwright, real backend, the 4 recipes end-to-end | 30–50 |

Today: **57 backend tests, 0 frontend tests.** The 12 Playwright specs that exist are
`page.goto` + "does the body contain this word," which is presence-testing, not behavior —
they get rewritten, not counted.

That is a real number worth internalizing: reaching 90% honestly means roughly **1,800–2,500
new tests**. It is the largest single workstream in this plan, and it is why tests ship with
each phase rather than after.

### Specific rules

- **Test the failure paths.** Provider down, provider returns 500, timeout, malformed SSE,
  empty logprobs, cancelled request, DB constraint violation, concurrent update. The audit
  found several bugs (`streamError`, `ProbeSucceeded`) that live entirely in failure paths.
- **No mocking what you own.** Mock the HTTP boundary (WireMock) and the clock. Use a real
  Postgres via Testcontainers for anything touching EF — in-memory provider hides exactly the
  bug class that `.Include("")` belongs to.
- **Deterministic.** Inject `TimeProvider` and seeded RNG (`SplitDatasetHandler:60` uses an
  unseeded `Random()` when no seed is given — for a *research* tool, unseeded splits are a
  reproducibility bug, not a style nit).
- **Migration round-trip test:** every migration applies up, down, and up again on a clean DB;
  and the EF model matches the migration snapshot (no pending model changes).
- **Snapshot the OpenAPI document**; any route change without a spec update fails CI.

---

## P7 — UX and onboarding

*Runs: cloud (fully verifiable here). ~4 weeks.*

Target user: a junior researcher who knows some ML but has never run a local inference server
and does not yet know what entropy over a truncated top-k distribution means.

**7.1 First-run wizard.** Today the app assumes a registered provider and shows a permanently
green "Connected" dot in `StatusBar.tsx` that is hardcoded and lies even with the backend down.
Replace with: detect running providers by probing `localhost:8000/11434/1234` → one-click
register → live capability probe → a plain-language readout ("Ollama detected. Token heatmaps
need logprobs, which this provider doesn't expose — here's how to switch to vLLM, or continue
with the features that work"). Fix the status bar to reflect real health, model, and GPU state.

**7.2 Capability-aware gating everywhere.** The single largest source of confusion. A control
that cannot work must be visibly disabled with a tooltip explaining which provider offers it —
never silently broken or silently empty. `product-truth.yaml` flags this for 6 modules.

**7.3 Empty states that teach.** Every list page with no data explains what the feature is
for, what a good result looks like, and offers a one-click "load example" — replacing the
current approach of pre-seeding fabricated rows that look like real results.

**7.4 The interpretation layer.** This is what separates "shows numbers" from "produces
research." Every metric gets an inline explainer: what it measures, its range, what a high or
low value *implies*, and how it misleads. Specifically: entropy here is over the **truncated
top-k set** and is not the full-distribution entropy — a junior researcher will otherwise
report it as if it were. Perplexity is not comparable across tokenizers. Logprob-derived
confidence is not calibrated confidence. Say all of this in the UI, at the point of use.

**7.5 The four guided recipes.** Each is a real in-app flow: framed question → guided steps →
interpretation → export.

1. **"Is my model guessing?"** — prompt → heatmap → find low-confidence regions → inspect
   alternatives → conclusion about where the model is uncertain and why.
2. **"Did my prompt change help?"** — A/B two prompt versions → sweep over seeds → per-token
   entropy delta → significance test → verdict with effect size and CI, not just a mean.
3. **"Why did RAG retrieve that?"** — ingest → inspect chunking → query → retrieval trace →
   citation mapping → compare vector vs FTS vs hybrid.
4. **"Is my model calibrated?"** — dataset → batch eval → reliability curve + ECE →
   cost-vs-quality frontier.

Each recipe: resumable, ~10 minutes, works on a laptop-class model, ends in an export.

**7.6 Foundations.** Semantic design tokens and a color-blind-safe palette (a *confidence
heatmap* that fails for 8% of male users is a correctness bug, not an aesthetic one); WCAG 2.1
AA with automated axe checks in CI; full keyboard navigation and a command palette; chart data
available as a table for screen readers; Storybook stories for every shared component;
loading/empty/error/partial states for every async view.

**7.7 Remove the dead ends.** `/coming-soon` is registered and unreachable; `Sidebar.tsx`'s
`!item.active` branch and `NavItem.phase` are dead code; `useSSE.ts` is unused; the sidebar
lists 15 modules while the README claims 14.

---

## P8 — The reproducible export

*Runs: cloud + host. ~1 week. This is the artifact that proves the whole thing worked.*

Every recipe terminates in an **export bundle** — the thing a junior researcher attaches to a
paper, sends to an advisor, or hands to a reviewer:

```
prism-export-<runId>/
  manifest.json      # schema-versioned: git SHA, Prism version, provider + endpoint,
                     # model ID + digest, all sampling params, seeds, tokenizer,
                     # dataset ID + content hash, prompt version, UTC timestamps
  report.md          # question, method, results, charts, interpretation, caveats
  report.pdf         # rendered
  data/*.parquet     # per-token logprobs, per-record scores, raw traces
  charts/*.svg       # vector, publication-quality
  replay.json        # feed back into Prism to re-run bit-for-bit
```

Two hard requirements, both tested:

- **Reproducibility test:** import `replay.json` on a clean instance against the same provider
  and model; assert metrics match within tolerance (exact for `temperature=0`). If it does not
  reproduce, the export is decoration.
- **Provenance completeness test:** a schema test asserting every field needed to reproduce is
  present and non-null — no run can export while missing its seed or model digest.

The report template enforces scientific hygiene: state the question, report n, report variance
not just means, name the confounds, and flag when a provider's capability limits weaken the
conclusion (e.g. "top-k=5 truncation means entropy is a lower bound").

---

## P9 — Ten falsification cycles

*Runs: host + cloud. ~2 weeks. Not "ten passes" — ten attempts to prove the work wrong.*

Rules:

- Each cycle has a **thesis to falsify**, not a checklist.
- Each cycle must either **kill an assumption or add a new one** to the ledger. A cycle that
  finds nothing means the probe was too weak — the next cycle escalates severity rather than
  declaring success.
- Anything found becomes a **permanent regression test** before it is fixed.
- Termination: **two consecutive cycles at maximum severity finding nothing new** — the ten is
  a budget, not a target. If cycle 10 is still finding real defects, that is the finding, and
  the release date is what moves.

| # | Falsifies | Method | Exit criterion |
|---|---|---|---|
| 1 | "It builds and the gates are real" | clean clone on a fresh machine, run every gate | all green from scratch, no local state |
| 2 | "The bugs are fixed" | re-run P1 tests against pre-fix commits; confirm they fail there | each test provably detects its bug |
| 3 | "Everything is recorded" | exercise all 15 UI surfaces; assert `InferenceRun`/`UsageLog` rows for each | zero un-recorded inference paths |
| 4 | "Jobs are durable" | chaos: kill workers, kill the DB, partition the network, restart mid-queue | exactly-once, no lost or duplicated work |
| 5 | "Provider claims are true" | contract suite against live vLLM + Ollama + LM Studio + an OpenAI-compatible endpoint | capability matrix matches observed behavior exactly |
| 6 | "The tests are meaningful" | Stryker full run; manually break 20 behaviors and confirm tests catch them | mutation ≥70%; 20/20 caught |
| 7 | "It survives real data" | 1M `UsageLog` rows, 100k RAG chunks, 10k-record datasets, 8-hour batch | p95 latency budgets hold; no OOM; no unindexed scans |
| 8 | "A junior can use it" | 3 unfamiliar people, no docs, no help, screen-recorded, timed | each completes ≥2 recipes in <15 min; every stumble becomes a UX ticket |
| 9 | "The research is defensible" | a domain expert reviews the exports from cycle 8 | conclusions supported by the data; caveats stated; independently reproduced from `replay.json` |
| 10 | "The docs are true" | line-by-line README audit against a running instance; every claim demoed | zero unsupported claims; `product-truth.yaml` regenerates identical |

Cycles 8 and 9 are the ones that actually answer your requirement. Everything before them
tests whether the software works; those two test whether *a person* can produce good research
with it — which is a different question, and it cannot be answered by any gate in CI.

---

## Sequencing, effort, and risk

**Effort** (one experienced engineer; agent-assisted parallelism compresses the test and UI
workstreams substantially):

| Phase | Weeks |
|---|---|
| P0 harness & truth | 1 |
| P1 bugs | 0.5 |
| P2 recording spine | 1.5 |
| P3 job runner | 2 |
| P4 module completion | 7 |
| P5 providers | 1 |
| P6 tests to 90% | 7 (interleaved) |
| P7 UX & onboarding | 4 |
| P8 export | 1 |
| P9 ten cycles | 2 |
| **Total** | **≈27 engineer-weeks** (~6 months solo; ~10–14 weeks with parallelism) |

**Critical path:** P0 → P2 → P3 → P4.1/P4.2 → P8 → P9. P7 can run in parallel from the start
(frontend, fully verifiable in the cloud sandbox); P6 is not a phase so much as a tax on every
other one.

**Risks, ranked:**

1. **A0 is unverified** — if the backend does not currently compile, P0 grows unpredictably.
   Verify this first, today, before committing to any schedule.
2. **The 90% target is 1,800–2,500 tests.** If the ratchet ever slips, it will not be
   recovered. The ratchet plus mutation floor is what makes the number mean something; without
   both, expect 90% coverage and no more real assurance than today.
3. **Live-provider tests are flaky by nature.** Cassettes in CI, live tests nightly, and never
   gate a PR on a GPU being warm.
4. **UX rework may invalidate tests.** Do P7's information architecture *before* writing the
   frontend test suite, or write those tests twice.
5. **Scope is genuinely large.** "Build everything to the claim" means eight modules going
   from entities to working systems. The honest alternative — trimming the README — was
   declined deliberately; that is a legitimate choice, but it is a two-quarter choice.

**One structural recommendation.** The root cause of this repo's condition is not any single
bug: it is that documentation could make claims no gate could falsify, so the docs drifted
from the code within six commits while an accurate `product-truth.yaml` sat right next to the
inaccurate README. P0.8 (truth-as-test) and P0.4 (mutation floor) are the two items that stop
it recurring. If only part of this plan is adopted, adopt those two first.

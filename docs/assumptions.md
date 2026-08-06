# Assumption Ledger

Nothing here is marked **proven** because code was read. Only because a command ran and its
output is recorded below.

**Environment:** cloud sandbox, x86_64 Linux · SDK 10.0.302 · runtimes 9.0.18 + 10.0.10 ·
PostgreSQL 16.13 + pgvector 0.6.0 (native, no Docker) · offline NuGet feed (146 packages) ·
`DOTNET_ROLL_FORWARD=Major` · commit `f6726f6`.

| ID | Assumption | Status | Evidence |
|---|---|---|---|
| A0 | Backend compiles on .NET 9 | **PROVEN** | `dotnet build Prism.sln` → `Build succeeded. 0 Warning(s) 0 Error(s)`, 29.8s, with `TreatWarningsAsErrors=true` |
| A1 | The existing tests pass | **PARTIAL** | 60 tests (not 56 as README says, not 32 as product-truth says): **56 pass, 4 fail**. All 4 failures are `JobStoreIntegrationTests` — environmental, not logic |
| A2 | `dotnet format --verify-no-changes` passes | **FALSIFIED** | exit 2 — `ForkTemplateHandler.cs(60,23): error WHITESPACE`. **CI's `backend-format` job is red on `main` today** |
| A3 | Migrations apply cleanly to Postgres 16 + pgvector | **PROVEN (conditional)** | All 35 migrations applied to an empty DB; 60/60 tests pass afterwards — but only once `PendingModelChangesWarning` is suppressed (see A16) |
| A16 | *(new)* The EF model matches the migrations snapshot | **FALSIFIED** | 13 pending operations: 6 GIN indexes dropped + recreated (`rag_chunks.search_vector`, `prompts_templates.Tags`, `experiments_runs.Metrics`, `experiments_runs.Tags`, `evaluation_results.Scores`, `datasets_records.Data`) plus `AlterDatabaseOperation` |
| A17 | *(new)* `AppDbContext` builds a complete model when constructed directly | **FALSIFIED** | Model registration depends on a **static mutable list**. 30 of 31 entity configs live in `Prism.Features` and are only visible after `AppDbContext.RegisterAssembly()` — called from exactly one place, `Prism.Api/Extensions/ServiceCollectionExtensions.cs:65` |
| A18 | *(new)* `DatabaseFixture` configures the context like the app does | **FALSIFIED** | Two independent gaps: never calls `RegisterAssembly` (empty model → 31 `DropTable` ops), and omits `npgsqlOptions.UseVector()` (model validation throws on `RagChunk.Embedding`) |
| A19 | *(new)* Migration failures are visible to an operator | **FALSIFIED** | `Program.cs:76-79` catches **all** exceptions from `MigrateAsync` and logs `"Could not apply database migrations. Is PostgreSQL running?"` — the app then serves traffic against a stale schema, with a misleading cause |
| A13 | Storybook/Playwright CI jobs are red | **PROVEN** (earlier) | packages absent from `package.json`, `node_modules`, and lockfile; both jobs reproduce as exit 1; `ci-summary` gates only 4 of 8 |
| A5 | `.Include("")` throws at runtime | UNPROVEN | needs a seeded RAG collection + live query |
| A6 | 10s timeout kills real generations | UNPROVEN | needs a delayed stub provider |
| A7 | Unassigned `streamError` loses the message | UNPROVEN | needs mid-stream fault injection |
| A8 | `UsageLog`/`EvaluationResult`/`BatchResult` never written | UNPROVEN | needs endpoint exercise + row counts |
| A9 | JupyterLite iframe 404s | UNPROVEN | needs Playwright against a running app |
| A10 | Prompt Lab version-diff 404s | UNPROVEN | needs endpoint integration test |
| A11 | Ollama cannot return logprobs | UNPROVEN | needs live `ollama serve` — **host only** |
| A12 | vLLM needs `guided_json` | UNPROVEN | needs live vLLM — **host only** |
| A14 | `dev.sh` works from a clean clone | UNPROVEN | needs Docker — **host only** |
| A15 | pgvector similarity is unindexed | UNPROVEN | needs `EXPLAIN ANALYZE` at 100k chunks |

## The two findings that change the plan

**1. `AppDbContext` resolves its schema from static mutable global state.**

```csharp
private static readonly List<Assembly> _additionalAssemblies = new();
public static void RegisterAssembly(Assembly assembly) { ... }
```

Whether the database model contains 1 entity or 31 depends on whether a static method was
called first, by whom, and in what order. It is not thread-safe, not order-independent, and
invisible at the call site. Any `AppDbContext` created outside the API host — tests, scripts,
tooling — silently gets a near-empty model.

The dangerous case: there is **no `IDesignTimeDbContextFactory`**, so `dotnet ef migrations add`
boots through `Program.cs`. If that path ever fails to register the Features assembly, EF
compares a full snapshot against an empty model and generates a migration that **drops all 30
tables**. I reproduced exactly that diff (31 operations, all `DropTable`) simply by constructing
the context the way `DatabaseFixture` does.

This belongs in Phase 0, ahead of the recording spine — replace the static registry with
explicit constructor injection or a proper design-time factory, and add a test asserting the
model contains all 31 entity types.

**2. Migration failure is indistinguishable from "Postgres isn't running."**

`Program.cs` catches everything and blames the database server. Today that hides A16's real
model drift behind an error message that sends the operator to `docker compose`. Narrow the
catch to connection failures and let schema errors be fatal — a server running on a schema
that doesn't match its model is worse than a server that refused to start.

## Corrections to the plan

- **P0.1 is narrower than written.** `backend/Directory.Build.props` already exists and already
  sets `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` and `GenerateDocumentationFile`.
  Only `global.json` and `.nvmrc` are missing. And warnings-as-errors is already on and clean,
  which is a genuinely good sign about the codebase.
- **P0 gains two items:** kill the static assembly registry (A17), and add the migration
  round-trip / no-pending-changes test (A16) — which I'd scheduled for P6 and which is
  cheap enough, and load-bearing enough, to belong in the first phase.
- **The test-count claim was wrong in three different places.** README says 56, `product-truth.yaml`
  says 32, my grep counted 57 attributes; the truth is **60**. The doc-truth test in P0.8 should
  assert against `dotnet test` output, not a hand-maintained number.
- **`DatabaseFixture` needs three fixes, not one:** the `PRISM_TEST_DB` override (validated —
  it took the suite from 56/60 to 60/60 with no Docker), plus `RegisterAssembly`, plus
  `UseVector()`.

## Sandbox notes

Reproduce with `PRISM_TEST_DB=Host=localhost;Port=5438;Database=prism_test;Username=postgres;Password=postgres`.

Local-only scaffolding, **not** part of any fix and not committed to the repo:
`backend/nuget.config` (offline feed) and `backend/Directory.Build.targets` (pins framework
packs to 9.0.14 because the sandbox SDK is 10.0.302 while the feed was restored by 10.0.201 —
which is itself an argument for the `global.json` pin).

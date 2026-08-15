# CLAUDE.md — Instructions for Claude Code

This file guides Claude Code when working on Prism. Read this before every task.

**This file describes the code as it is, not as it is planned.** Where the two differ, the
code wins and this file is the bug. `ARCHITECTURE.md` and the ADRs describe intended design
and are *not* reliable as a description of what exists — several document abstractions that
were never implemented. Verify against the source before coding against anything they claim.

## Project Context

This is an all-in-one AI Research platform. See `DESIGN.md` for vision, `ARCHITECTURE.md` for
intended structure, `PROJECT_PLAN.md` for tasks. ADRs are in `docs/ADR/`.

**Stack:** .NET 9 Minimal API | React + TypeScript + Vite | PostgreSQL + pgvector | EF Core | Serilog + OpenTelemetry

**Solution layout** (`backend/Prism.sln`) — four projects:

| Project | Contents |
|---|---|
| `src/Prism.Api` | Host only: `Program.cs`, DI wiring, 3 middleware. 7 files. |
| `src/Prism.Features` | All 16 feature slices. |
| `src/Prism.Common` | Shared abstractions, providers, EF migrations, job runner. |
| `src/Prism.Tests` | Unit + integration tests (one project). |

## Architecture Rules (Non-Negotiable)

1. **Vertical Slice Architecture.** Code goes in `Features/{FeatureName}/`. Never create a top-level `Services/`, `Repositories/`, or `Controllers/` folder.
2. **Clean Architecture within each slice.** Four sub-folders: `Domain/`, `Application/`, `Infrastructure/`, `Api/`. Dependencies flow inward: Api -> Application -> Domain. Infrastructure implements Application interfaces.
3. **Result<T> pattern.** All application-layer methods return `Result<T>`. Never throw exceptions for expected failures. Use `Error.NotFound()`, `Error.Validation()`, etc. See `ARCHITECTURE.md` Result Pattern section.
4. **Provider abstractions.** Use these interfaces rather than concrete implementations in feature code:

   | Concern | Interface | Implementations |
   |---|---|---|
   | Database | `AppDbContext` (EF Core) | single context, `Common/Database/` |
   | Inference | `IInferenceProvider` | `Common/Inference/Providers/` (Ollama, vLLM, OpenAI-compatible) |
   | Cache | `ICacheService` | `Common/Cache/Providers/` (InMemory, Redis, Null) |
   | Storage | `IFileStorage` | `Common/Storage/Providers/` (Local, Null) |
   | Auth | `ICurrentUser` | `Common/Auth/CurrentUser.cs` |

   Never use `HttpClient` to an inference server, `IMemoryCache`, `System.IO.File`, or raw auth
   headers directly in feature code.

   **There is no vector-store, global-search, or export abstraction.** `IVectorStore`,
   `IGlobalSearch` and `IExportService` exist as interface files in `Common/` but have **no
   implementation and no DI registration anywhere**. Do not code against them and do not
   inject them — the container cannot resolve them. What the code actually does:

   - **Vectors:** RAG stores embeddings as `Pgvector` columns on its own entities, configured
     in `Features/Rag/Infrastructure/RagChunkConfiguration.cs`, and queries them through
     `AppDbContext` with LINQ. Embedding generation is
     `Features/Rag/Infrastructure/OpenAiEmbeddingProvider.cs`.
   - **Search:** per-feature. History search is
     `Features/History/Application/SearchHistory/SearchHistoryHandler.cs`. RAG chunks carry a
     `SearchVector` tsvector computed column with a GIN index, declared in the EF configuration.
   - **Export:** per-feature handlers, six of them, named `Export*Handler.cs` under
     `Features/{Feature}/Application/Export*/`. They share only the `ExportFormat` enum in
     `Common/Export/`.

5. **XML doc comments.** Every public type, method, interface, and property. `<summary>` is mandatory. `<param>`, `<returns>`, `<example>` where appropriate. Compiler-enforced via `<TreatWarningsAsErrors>`.
6. **Minimal API endpoints.** Use route groups, `TypedResults`, and endpoint filters. No MVC controllers.
7. **Feature-prefixed tables.** EF Core entity tables: `{feature}_{entity}` (e.g., `playground_sessions`, `experiments_runs`).
8. **There are no migrations. The entity configurations are the schema.** To change the
   schema, change the `IEntityTypeConfiguration<T>` and recreate the database — never add a
   migration, and never edit a database in place. `SchemaBootstrapper.EnsureSchemaAsync`
   creates the schema from the model on start and records a hash of it; if the database no
   longer matches, the API refuses to start and says so. Run `./dev.sh` and answer yes to
   "initialise the database", which drops every table and reloads the seeders. All data is
   reproducible from the seeders, so nothing is lost.

## Code Style

- **C# naming:** PascalCase for public members, _camelCase for private fields, camelCase for local variables and parameters.
- **Records for DTOs and value objects.** Use `sealed record` for immutable data. Use `sealed class` for entities with identity.
- **Async all the way.** All I/O methods are async with `CancellationToken ct` as the last parameter.
- **No `var` for non-obvious types.** Use explicit types when the type isn't clear from the right side of the assignment.
- **Structured logging.** Always use named properties: `Log.Information("Loaded {ModelName} in {DurationMs}ms", name, ms)`. Never string interpolation.
- **TypeScript:** Strict mode. No `any`. Prefer `interface` over `type` for object shapes.

## The Frontend API Client Is Hand-Written

`orval` is configured (`frontend/orval.config.ts`, `npm run api:generate`) but **its output
directory `frontend/src/services/generated/` is gitignored and has never existed in a
checkout.** The config also reads its schema from a live `http://localhost:5000/swagger/v1/swagger.json`,
so generation only works with the API already running.

What exists instead:

- `frontend/src/services/apiClient.ts` — hand-written fetch wrapper. All calls go through it.
- `frontend/src/services/types/` — three hand-maintained type files (`common`, `inference`, `logprobs`).
- `frontend/src/services/mutationErrors.ts` — error normalisation.

Nothing type-checks these against the API, but **the API surface itself is now pinned**.
`frontend/openapi.json` is committed, and the `openapi-drift` CI job re-exports the document
and fails the build when the two disagree:

```
dotnet run --project backend/src/Prism.Api -- --export-openapi "$PWD/frontend/openapi.json"
```

That tells you *that* the surface moved. It cannot tell you the TypeScript was updated to
match — that part is still on you.

**Therefore:** when you change an endpoint's request or response shape, re-export the
document, update the corresponding type in `services/types/`, fix the call site, and commit
all of it together. Grep `frontend/src/features/{name}/` for the route string. A red
`openapi-drift` job means you changed the API and forgot the client.

## How to Implement a Research Capability

Anything that computes a metric, statistic or aggregate a researcher would cite — BLEU, ECE,
nDCG, perplexity, cost — follows `docs/prompts/IMPLEMENT_RESEARCH_CAPABILITY.md` exactly. It
defines what proof means here: reference vectors from a published source, invariants that hold
for all inputs, and a hand-worked example. A passing test only shows the code agrees with itself.

The plan those capabilities come from is `docs/plans/RESEARCH_CAPABILITIES.md`.

## How to Create a New Feature Slice

See `SKILLS.md` (Claude Code skills) for the step-by-step procedure. The short version:

```
Features/
  {FeatureName}/
    Domain/
      {Entity}.cs                    # Aggregate root / entities
    Application/
      {UseCase}/
        {UseCase}Command.cs          # or Query.cs
        {UseCase}Handler.cs
        {UseCase}Validator.cs        # FluentValidation
      Dtos/
        {Entity}Dto.cs
    Infrastructure/
      {Entity}Configuration.cs      # IEntityTypeConfiguration<T>
      {Feature}Repository.cs        # if needed beyond DbContext
    Api/
      {Feature}Endpoints.cs         # MapGroup + route definitions
      Requests/
        {Request}Request.cs
      Responses/
        {Response}Response.cs
    {Feature}Module.cs               # DI registration: Add{Feature}Feature()
```

Register in `src/Prism.Api/Extensions/ServiceCollectionExtensions.cs`:
`services.Add{Feature}Feature()`, and map the endpoints from `WebApplicationExtensions.cs`.

There is no mediator. `Handler` classes are plain DI-registered classes called directly from
endpoints — do not add MediatR or reach for `IMediator`.

## How to Create a New ADR

1. Find the next number: `ls docs/ADR/*.md | sort`
2. Copy `docs/ADR/template.md` to `docs/ADR/{NNN}-{slug}.md`
3. Fill in all sections — especially Alternatives Considered
4. Add to the table in `docs/README.md`
5. Reference from `ARCHITECTURE.md` where relevant

**An ADR marked `Accepted` means the decision was taken, not that the code does it.** ADR-009
(vector store abstraction) is the cautionary example: accepted, documented in detail, never
built. If you implement or abandon an ADR, update its Status.

## Testing

- **Unit tests:** `src/Prism.Tests/Unit/{Area}/` — test handlers with mocked dependencies
- **Integration tests:** `src/Prism.Tests/Integration/` — Testcontainers for Postgres, `Support/FakeHttpTransport.cs` for inference
- **Fake providers:** Use `NullCacheService`, `NullFileStorage` for tests
- **No test for trivial code.** Don't test DTOs, record constructors, or mapping-only code.

Coverage is uneven and you should not assume a slice is protected. Verified 2026-08-15:

- **No unit-test folder at all:** Agents, Notebooks, FineTuning, Analytics, TokenExplorer,
  BatchInference — and History, whose coverage is integration-only.
- **Has unit tests:** Inference (12 files), Evaluation (3), Models (5), Rag (2), and one file
  each for Api, Capabilities, Composition, Datasets, Experiments, PromptLab, StructuredOutput,
  Workspaces.

When changing an uncovered slice, add the test you wish had been there.

## Common Mistakes to Avoid

- **Don't create a `Services/` folder.** Logic goes in `Application/{UseCase}/Handler.cs`.
- **Don't return `IActionResult`.** Use `TypedResults.Ok()`, `TypedResults.NotFound()`, etc.
- **Don't catch exceptions in handlers.** Return `Result.Failure()`. Let `GlobalExceptionMiddleware` handle unexpected ones.
- **Don't inject `IVectorStore`, `IGlobalSearch` or `IExportService`.** They have no implementations; the container will throw at resolution time. See rule 4.
- **Prefer LINQ over raw SQL in feature code**, and configure column types in `IEntityTypeConfiguration<T>`. The one sanctioned exception is BM25 lexical scoring in `Features/Rag/Application/QueryCollection/QueryCollectionHandler.cs`, which uses `SqlQueryRaw` because LINQ cannot express `ts_rank`. Adding a second exception needs a reason in the PR.
- **Don't hand-write a *new* fetch layer.** Extend `services/apiClient.ts` and update `services/types/` — and see the API-client section above, because there is no generator to fall back on.
- **Don't add an EF migration.** There are none, by design. Change the entity configuration and reinitialise the database — see rule 8.
- **Don't add a new DbContext.** Use the single `AppDbContext`. Add your `IEntityTypeConfiguration<T>` — it's auto-discovered via `Common/Database/ModelAssemblies.cs`, which points EF at the Features assembly through `Prism.Features/Marker.cs`.
- **Don't skip the CancellationToken.** Every async method takes `CancellationToken ct` and passes it through.
- **Don't trust a provider capability claim without checking the probe.** Most historical defects in this repo are a UI, seed row, or doc asserting a capability (logprobs, guided decoding, embeddings) that the configured provider does not have. Capability truth lives in `Common/Inference/Capabilities/` and `Features/Models/Infrastructure/ProviderCapabilityRegistry.cs`.

## File Locations Quick Reference

Backend paths are relative to `backend/`.

| What | Where |
|------|-------|
| DI composition root | `src/Prism.Api/Program.cs` + `src/Prism.Api/Extensions/ServiceCollectionExtensions.cs` |
| Middleware | `src/Prism.Api/Middleware/` |
| Shared abstractions | `src/Prism.Common/Abstractions/` |
| Result pattern | `src/Prism.Common/Results/` |
| Provider interfaces | `src/Prism.Common/{Provider}/I{Provider}.cs` |
| Provider implementations | `src/Prism.Common/{Cache,Storage,Inference}/Providers/`; auth is `Common/Auth/CurrentUser.cs` |
| Inference runtime + capabilities | `src/Prism.Common/Inference/{Runtime,Capabilities,Metrics}/` |
| Durable job runner | `src/Prism.Common/Jobs/` |
| Feature slices | `src/Prism.Features/{Name}/` |
| Schema creation + staleness guard | `src/Prism.Common/Database/SchemaBootstrapper.cs` |
| Seed runner | `src/Prism.Common/Database/Seeders/`; per-feature seeders are `Features/{Name}/Infrastructure/{Name}Seeder.cs` |
| Frontend features | `frontend/src/features/{name}/` |
| API client (hand-written) | `frontend/src/services/apiClient.ts` + `frontend/src/services/types/` |
| OpenAPI export | `src/Prism.Api/OpenApiExport.cs`; baseline at `frontend/openapi.json` |
| Vendored offline toolchain | `toolchain/` (gitignored; built by `scripts/handoff-to-sandbox.sh`) |
| ADRs | `docs/ADR/` |

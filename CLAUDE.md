# CLAUDE.md — Instructions for Claude Code

This file guides Claude Code when working on the AI Research Workbench. Read this before every task.

## Project Context

This is an all-in-one AI Research platform. See `DESIGN.md` for vision, `ARCHITECTURE.md` for structure, `PROJECT_PLAN.md` for tasks. ADRs are in `docs/ADR/`.

**Stack:** .NET 9 Minimal API | React + TypeScript + Vite | PostgreSQL + pgvector | EF Core | Serilog + OpenTelemetry

## Architecture Rules (Non-Negotiable)

1. **Vertical Slice Architecture.** Code goes in `Features/{FeatureName}/`. Never create a top-level `Services/`, `Repositories/`, or `Controllers/` folder.
2. **Clean Architecture within each slice.** Four sub-folders: `Domain/`, `Application/`, `Infrastructure/`, `Api/`. Dependencies flow inward: Api -> Application -> Domain. Infrastructure implements Application interfaces.
3. **Result<T> pattern.** All application-layer methods return `Result<T>`. Never throw exceptions for expected failures. Use `Error.NotFound()`, `Error.Validation()`, etc. See `ARCHITECTURE.md` Result Pattern section.
4. **Provider abstractions.** Never use concrete implementations directly in feature code:
   - Database: `AppDbContext` (EF Core) — never raw SQL in features
   - Inference: `IInferenceProvider` — never `HttpClient` to vLLM directly
   - Cache: `ICacheService` — never `IMemoryCache` or Redis directly
   - Storage: `IFileStorage` — never `System.IO.File` directly
   - Auth: `ICurrentUser` — never read auth headers directly
   - Vectors: `IVectorStore` — never pgvector SQL directly
   - Search: `IGlobalSearch` — never tsvector queries in features
5. **XML doc comments.** Every public type, method, interface, and property. `<summary>` is mandatory. `<param>`, `<returns>`, `<example>` where appropriate. Compiler-enforced via `<TreatWarningsAsErrors>`.
6. **Minimal API endpoints.** Use route groups, `TypedResults`, and endpoint filters. No MVC controllers.
7. **Feature-prefixed tables.** EF Core entity tables: `{feature}_{entity}` (e.g., `playground_sessions`, `experiments_runs`).

## Code Style

- **C# naming:** PascalCase for public members, _camelCase for private fields, camelCase for local variables and parameters.
- **Records for DTOs and value objects.** Use `sealed record` for immutable data. Use `sealed class` for entities with identity.
- **Async all the way.** All I/O methods are async with `CancellationToken ct` as the last parameter.
- **No `var` for non-obvious types.** Use explicit types when the type isn't clear from the right side of the assignment.
- **Structured logging.** Always use named properties: `Log.Information("Loaded {ModelName} in {DurationMs}ms", name, ms)`. Never string interpolation.
- **TypeScript:** Strict mode. No `any`. Prefer `interface` over `type` for object shapes. Use generated API client from orval — don't hand-write fetch calls.

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

Register in `Program.cs`: `services.Add{Feature}Feature()` and `app.Map{Feature}Endpoints()`.

## How to Create a New ADR

1. Find the next number: `ls docs/ADR/*.md | sort`
2. Copy `docs/ADR/template.md` to `docs/ADR/{NNN}-{slug}.md`
3. Fill in all sections — especially Alternatives Considered
4. Add to the table in `docs/README.md`
5. Reference from `ARCHITECTURE.md` where relevant

## Testing

- **Unit tests:** `Tests/Unit/Features/{Feature}/` — test handlers with mocked dependencies
- **Integration tests:** `Tests/Integration/` — use Testcontainers for Postgres, WireMock for inference
- **Fake providers:** Use `FakeInferenceProvider`, `NullCacheService`, `NullFileStorage` for tests
- **No test for trivial code.** Don't test DTOs, record constructors, or mapping-only code.

## Common Mistakes to Avoid

- **Don't create a `Services/` folder.** Logic goes in `Application/{UseCase}/Handler.cs`.
- **Don't return `IActionResult`.** Use `TypedResults.Ok()`, `TypedResults.NotFound()`, etc.
- **Don't catch exceptions in handlers.** Return `Result.Failure()`. Let `GlobalExceptionMiddleware` handle unexpected ones.
- **Don't add Postgres-specific SQL in feature code.** Configure column types in `IEntityTypeConfiguration<T>`. Query via LINQ.
- **Don't hand-write API client code.** Run `npm run api:generate` after adding/changing endpoints.
- **Don't add a new DbContext.** Use the single `AppDbContext`. Add your `IEntityTypeConfiguration<T>` — it's auto-discovered.
- **Don't skip the CancellationToken.** Every async method takes `CancellationToken ct` and passes it through.

## File Locations Quick Reference

| What | Where |
|------|-------|
| DI composition root | `Api/Program.cs` |
| Middleware | `Api/Middleware/` |
| Shared abstractions | `Common/Abstractions/` |
| Result pattern | `Common/Results/` |
| Provider interfaces | `Common/{Provider}/I{Provider}.cs` |
| Provider implementations | `Common/{Provider}/Providers/` |
| Feature slices | `Features/{Name}/` |
| EF migrations | `Common/Database/Migrations/` |
| Seed data | `Common/Database/Seeders/` |
| Frontend features | `frontend/src/features/{name}/` |
| Generated API client | `frontend/src/services/generated/` |
| ADRs | `docs/ADR/` |

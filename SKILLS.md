# Claude Code Skills — AI Research Workbench

Step-by-step procedures for common development tasks. Follow these exactly.

> **Note:** The application's own skill system (ISkill, SkillRegistry) is documented in `docs/PLATFORM_SKILLS.md`.

---

## Skill: Create a New Feature Slice

**When:** Adding a new module/feature to the platform.

### Steps

1. **Create the folder structure:**
```
Features/{FeatureName}/
  Domain/
  Application/
  Infrastructure/
  Api/
  {FeatureName}Module.cs
```

2. **Define domain entities** in `Domain/`:
```csharp
/// <summary>
/// {What this entity represents}. Aggregate root for the {Feature} feature.
/// </summary>
public sealed class {Entity} : BaseEntity
{
    // Properties with XML docs
}
```

3. **Create the first use case** in `Application/{UseCase}/`:
   - `{UseCase}Command.cs` (or `Query.cs`) — sealed record with parameters
   - `{UseCase}Handler.cs` — returns `Result<T>`, injected dependencies via constructor
   - `{UseCase}Validator.cs` — FluentValidation rules

4. **Create DTOs** in `Application/Dtos/`:
   - Sealed records mapping domain entities to API-safe shapes
   - Include a `static {Dto} FromEntity({Entity} entity)` factory method

5. **Create EF configuration** in `Infrastructure/`:
```csharp
public sealed class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.ToTable("{feature}_{entities}");  // feature-prefixed, snake_case, plural
        builder.HasKey(e => e.Id);
        // Configure properties, relationships, indexes
    }
}
```

6. **Create endpoints** in `Api/{Feature}Endpoints.cs`:
```csharp
public static class {Feature}Endpoints
{
    public static IEndpointRouteBuilder Map{Feature}Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/{feature-kebab-case}")
            .WithTags("{FeatureName}")
            .RequireAuthorization();

        group.MapPost("/", Create)
            .WithName("Create{Entity}")
            .Produces<{Entity}Dto>(201)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> Create(
        [FromBody] {CreateRequest} request,
        {UseCase}Handler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new {Command}(request), ct);
        return result.Match(
            dto => TypedResults.Created($"/api/v1/{feature}/{dto.Id}", dto),
            error => error.ToHttpResult());
    }
}
```

7. **Create the module registration** in `{FeatureName}Module.cs`:
```csharp
public static class {FeatureName}Module
{
    public static IServiceCollection Add{FeatureName}Feature(this IServiceCollection services)
    {
        services.AddScoped<{UseCase}Handler>();
        services.AddScoped<IValidator<{Command}>, {UseCase}Validator>();
        return services;
    }
}
```

8. **Register in composition root:**
   - `Program.cs` → `services.Add{FeatureName}Feature();`
   - `WebApplicationExtensions.cs` → `app.Map{FeatureName}Endpoints();`

9. **Generate migration:**
   ```bash
   dotnet ef migrations add Add{FeatureName}Tables --project src/AiResearch.Common --startup-project src/AiResearch.Api
   ```

10. **Regenerate frontend API client:**
    ```bash
    cd frontend && npm run api:generate
    ```

---

## Skill: Add a New Endpoint to an Existing Feature

**When:** Adding a route to an existing feature slice.

### Steps

1. **Create command/query + handler + validator** in `Application/{UseCase}/`
2. **Add the route** to the existing `{Feature}Endpoints.cs` MapGroup
3. **Add request/response types** if needed in `Api/Requests/` or `Api/Responses/`
4. **Register handler** in `{Feature}Module.cs`
5. **Regenerate API client:** `cd frontend && npm run api:generate`

---

## Skill: Add a New Provider Implementation

**When:** Adding a new backend for an existing abstraction (e.g., new cache provider, new inference engine).

### Steps

1. **Create the implementation** in `Common/{Abstraction}/Providers/{ProviderName}.cs`
2. **Implement the interface** — every method, with full XML docs
3. **Add config section** to `appsettings.json` under the abstraction key
4. **Register in DI** — add a case to the `AddCommon{Abstraction}(config)` switch statement
5. **Update the provider capability/compatibility matrix** in `ARCHITECTURE.md`
6. **Create or update the relevant ADR** if the abstraction design changes
7. **Add integration tests** in `Tests/Integration/Infrastructure/`

---

## Skill: Add a New Entity to an Existing Feature

**When:** Adding a new table/entity within an existing feature slice.

### Steps

1. **Create entity** in `Features/{Feature}/Domain/{Entity}.cs` — extend `BaseEntity`
2. **Create EF configuration** in `Features/{Feature}/Infrastructure/{Entity}Configuration.cs`
   - Table name: `{feature}_{entities}` (feature prefix, snake_case, plural)
3. **Generate migration:**
   ```bash
   dotnet ef migrations add Add{Entity}To{Feature} --project src/AiResearch.Common --startup-project src/AiResearch.Api
   ```
4. **Review the generated migration** for data loss, missing indexes, correct nullability
5. **Create DTO** in `Application/Dtos/{Entity}Dto.cs` if entity is exposed via API

---

## Skill: Add a New ADR

**When:** Making a significant architectural decision.

### Steps

1. **Determine next number:** check `docs/ADR/` for highest existing number
2. **Copy template:** `docs/ADR/template.md` → `docs/ADR/{NNN}-{kebab-case-title}.md`
3. **Fill all sections:**
   - Context — what problem are we solving?
   - Decision — what did we decide and why?
   - Consequences — positive, negative, neutral
   - Alternatives Considered — table with Pros/Cons/Why Not
   - References — links to relevant docs
4. **Update `docs/README.md`** — add row to ADR table
5. **Reference from `ARCHITECTURE.md`** — add "See ADR-{NNN}" where relevant

---

## Skill: Create a Database Migration

**When:** Schema changes (new tables, columns, indexes).

### Steps

1. **Make entity/configuration changes** first
2. **Generate migration:**
   ```bash
   dotnet ef migrations add {DescriptiveName} --project src/AiResearch.Common --startup-project src/AiResearch.Api
   ```
3. **Review the generated migration** — check for data loss, verify UP and DOWN
4. **Test:** migration applies cleanly on empty DB and existing DB
5. **Never edit a migration that has been applied** — create a new one instead

---

## Skill: Write a Handler (Use Case)

**When:** Implementing business logic for a feature.

### Template

```csharp
/// <summary>
/// {What this handler does}. {Why it exists}.
/// </summary>
public sealed class {UseCase}Handler
{
    private readonly AppDbContext _db;
    private readonly ILogger<{UseCase}Handler> _logger;
    // other dependencies

    public {UseCase}Handler(AppDbContext db, ILogger<{UseCase}Handler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// {What this method does}.
    /// </summary>
    /// <param name="command">{What the command contains}.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// {What success looks like}. Returns {ErrorType} if {failure condition}.
    /// </returns>
    public async Task<Result<{Dto}>> HandleAsync({Command} command, CancellationToken ct)
    {
        // 1. Validate / load dependencies
        var entity = await _db.{Entities}.FindAsync([command.Id], ct);
        if (entity is null)
            return Error.NotFound($"{Entity} '{command.Id}' not found.");

        // 2. Execute domain logic
        entity.DoSomething(command.Value);

        // 3. Persist
        await _db.SaveChangesAsync(ct);

        // 4. Log
        _logger.LogInformation("Did something to {EntityId}", entity.Id);

        // 5. Return DTO
        return {Dto}.FromEntity(entity);
    }
}
```

### Rules
- Return `Result<T>`, never throw
- Always accept `CancellationToken ct` and pass it to every async call
- Log at `Information` for significant actions, `Debug` for details, `Warning` for degradation
- Use structured log properties, never string interpolation
- Keep handlers focused — one use case per handler

---

## Skill: Write an Endpoint

**When:** Exposing a handler via HTTP.

### Template

```csharp
group.MapPost("/{route}", async (
    [FromBody] {Request} request,       // or [FromRoute], [FromQuery]
    {UseCase}Handler handler,           // injected from DI
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new {Command}(request), ct);
    return result.ToHttpResult();       // or result.Match(...) for custom mapping
})
.WithName("{OperationName}")
.WithSummary("{What this endpoint does}")
.Produces<{ResponseDto}>(200)           // or 201 for creation
.ProducesValidationProblem()            // 400
.ProducesProblem(404);                  // if applicable
```

### Rules
- Use `TypedResults` for compile-time safety
- Declare `.Produces<T>()` for OpenAPI generation
- Use `[FromBody]` explicitly — no magic binding
- One handler call per endpoint — no business logic in the endpoint itself
- Always include `CancellationToken ct`

---

## Skill: Write Frontend Feature Code

**When:** Building UI for a feature.

### Steps

1. **Regenerate API client** if backend changed: `npm run api:generate`
2. **Create page component** in `frontend/src/features/{feature}/{Feature}Page.tsx`
3. **Use generated hooks** from `@/services/generated/{feature}` — no hand-written fetch
4. **Follow patterns:**
   - Loading: `<LoadingSkeleton />` while data loads
   - Empty: `<EmptyState />` with call-to-action when no data
   - Error: toast for transient, inline for validation
   - Forms: React Hook Form + Zod schema
5. **Add route** in `app/routes.tsx`
6. **Add sidebar entry** in `components/layout/Sidebar.tsx`

---

## Skill: Add Provider Abstraction Config Swap

**When:** Making an infrastructure component swappable via configuration.

### Pattern

```csharp
// In ServiceCollectionExtensions
public static IServiceCollection AddCommon{Thing}(
    this IServiceCollection services, IConfiguration config)
{
    var provider = config.GetValue<string>("{Thing}:Provider") ?? "Default";

    switch (provider)
    {
        case "ProviderA":
            services.AddSingleton<I{Thing}, ProviderAImplementation>();
            break;
        case "ProviderB":
            services.AddSingleton<I{Thing}, ProviderBImplementation>();
            break;
        default:
            services.AddSingleton<I{Thing}, DefaultImplementation>();
            break;
    }

    return services;
}
```

```json
// appsettings.json
{
  "{Thing}": {
    "Provider": "ProviderA",
    "ProviderA": { /* provider-specific config */ },
    "ProviderB": { /* provider-specific config */ }
  }
}
```

### Checklist
- [ ] Interface in `Common/{Thing}/I{Thing}.cs` with full XML docs
- [ ] Default implementation that works with zero config
- [ ] Null/no-op implementation for testing
- [ ] Config section in `appsettings.json`
- [ ] DI registration with switch on config value
- [ ] ADR documenting the decision
- [ ] Entry in `ARCHITECTURE.md`

---

## Skill: Review Code for Architecture Compliance

**When:** Reviewing a PR or checking your own work.

### Checklist

- [ ] Code is in the correct feature slice folder
- [ ] No cross-feature direct dependencies (use shared abstractions in Common/)
- [ ] All public members have XML doc comments
- [ ] Handlers return `Result<T>`, not exceptions
- [ ] No concrete provider usage in feature code (no `System.IO.File`, no `IMemoryCache`, no raw SQL)
- [ ] Endpoints use `TypedResults` and declare `.Produces<T>()`
- [ ] Async methods accept `CancellationToken ct`
- [ ] Logging uses structured properties, not interpolation
- [ ] Entity tables are feature-prefixed: `{feature}_{entity}`
- [ ] New entities have `IEntityTypeConfiguration<T>` (not fluent API in DbContext)
- [ ] No `var` for non-obvious types
- [ ] Tests exist for non-trivial handler logic

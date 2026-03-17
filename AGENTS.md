# Claude Code Agents — Prism

Specialized modes for different types of work. When a task matches an agent's description, adopt that agent's focus and constraints.

> **Note:** The application's own agent system (platform agents, user-built agents, execution engine) is documented in `docs/PLATFORM_AGENTS.md`.

---

## Agent: Feature Builder

**Trigger:** "Add feature X", "Implement Y", "Build the Z module"

**Focus:** End-to-end feature slice creation following vertical slice architecture.

### Behavior

1. Read `ARCHITECTURE.md` to confirm project structure and patterns
2. Read `PROJECT_PLAN.md` to find the relevant task breakdown
3. Follow the "Create a New Feature Slice" skill in `SKILLS.md` exactly
4. Create all layers: Domain -> Application -> Infrastructure -> Api
5. Register in DI composition root
6. Generate migration if new entities are involved
7. Remind user to run `npm run api:generate` for frontend client

### Constraints

- Never skip the Domain layer even if it seems trivial — entities belong there
- Never put business logic in endpoints — it goes in handlers
- Every new file gets XML doc comments
- Ask before creating a migration — confirm entity design first

### Outputs

- Feature slice folder with all four layers
- DI registration in Module file
- Migration (if approved)
- Summary of what was created and what the user should do next (frontend, tests)

---

## Agent: Endpoint Implementer

**Trigger:** "Add an endpoint for X", "I need an API for Y"

**Focus:** Adding endpoints to existing features with proper request/response types.

### Behavior

1. Read the existing feature's endpoints file to understand conventions
2. Create command/query + handler + validator in Application/
3. Add the route to the existing MapGroup
4. Declare all `.Produces<T>()` metadata for OpenAPI
5. Register handler and validator in the feature's Module file

### Constraints

- One handler per endpoint — no inline logic
- Always use `TypedResults`, never `Results`
- Always include `CancellationToken ct`
- Return `result.ToHttpResult()` — don't manually map HTTP status codes

---

## Agent: Architecture Guardian

**Trigger:** "Review this code", "Does this follow our patterns?", "Check architecture"

**Focus:** Verifying code follows the project's architecture rules.

### Behavior

1. Read the code under review
2. Check against the compliance checklist in `SKILLS.md` ("Review Code for Architecture Compliance")
3. Flag violations with:
   - **What's wrong** (specific line/file)
   - **Why it matters** (which principle is violated)
   - **How to fix it** (concrete suggestion)
4. Check cross-references: is the feature registered in DI? Are endpoints mapped?

### Constraints

- Don't refactor unrelated code — focus on the changes
- Don't add features beyond what was asked
- Be specific: cite the ADR or ARCHITECTURE.md section that applies

### Red Flags to Always Catch

- `System.IO.File` or `Directory` in feature code → should use `IFileStorage`
- `IMemoryCache` in feature code → should use `ICacheService`
- Raw SQL in feature code → should use EF Core LINQ
- `throw new Exception` in a handler → should return `Result.Failure()`
- Missing `CancellationToken` on async methods
- String interpolation in log calls
- Missing XML doc comments on public members
- Business logic in endpoint delegates

---

## Agent: Provider Integrator

**Trigger:** "Add support for X provider", "Integrate with Y", "Swap out Z"

**Focus:** Implementing a new provider behind an existing abstraction.

### Behavior

1. Read the interface definition in `Common/{Abstraction}/`
2. Read existing implementations to match conventions
3. Implement all interface methods with full XML docs
4. Add configuration section to appsettings pattern
5. Add DI registration case in the switch statement
6. Update the provider capability/compatibility matrix in `ARCHITECTURE.md`
7. Create or update ADR if the abstraction needs to change
8. Write integration tests

### Constraints

- Never change the interface to accommodate a single provider — use capability flags
- Provider-specific code stays in the provider class — never leaks into Common or Features
- If the provider doesn't support a capability, return `Error.Unavailable(...)` with a clear message
- Always include a config example in appsettings comments

---

## Agent: Debugger

**Trigger:** "This doesn't work", "I'm getting an error", "Fix bug X"

**Focus:** Systematic diagnosis and minimal fix.

### Behavior

1. **Reproduce:** Understand the error — read error messages, stack traces, logs
2. **Locate:** Find the relevant code (use Grep/Glob, read the handler and endpoint)
3. **Diagnose:** Trace the data flow: endpoint -> handler -> provider/db -> response
4. **Fix:** Make the minimal change that addresses the root cause
5. **Verify:** Explain what was wrong and why the fix works

### Constraints

- Don't refactor while debugging — fix the bug, nothing else
- Don't add error handling "just in case" — only handle the actual failure mode
- If the bug reveals a pattern violation (e.g., missing Result<T>), note it but fix separately
- Check that the fix doesn't break the Result pattern or skip validation

---

## Agent: Migration Planner

**Trigger:** "Add a new table", "Change the schema", "Add a column"

**Focus:** Safe database schema changes with proper EF Core migrations.

### Behavior

1. Confirm the entity design with the user before generating a migration
2. Create/modify `IEntityTypeConfiguration<T>` in the feature's Infrastructure/
3. Use feature-prefixed table names: `{feature}_{entities}`
4. Generate the migration with a descriptive name
5. Review the generated migration for:
   - Data loss (dropping columns/tables)
   - Missing indexes on foreign keys
   - Correct nullable/required settings
   - Proper DOWN migration (rollback should work)
6. Flag any destructive changes and ask for confirmation

### Constraints

- Never edit a migration that has already been applied — create a new one
- Always include both UP and DOWN in review
- Feature-prefix all table names
- Use `jsonb` for flexible data (configured in entity config, not raw SQL)

---

## Agent: Documentation Writer

**Trigger:** "Document X", "Create an ADR for Y", "Update the docs"

**Focus:** Accurate, concise documentation that follows project conventions.

### Behavior

1. Read the existing documentation to match style and structure
2. Follow the ADR template in `docs/ADR/template.md` for architectural decisions
3. Cross-reference: if updating ARCHITECTURE.md, check if ADRs need updating too
4. Update `docs/README.md` index when adding new ADRs
5. Update `MEMORY.md` if the change affects project-level knowledge

### Constraints

- ADRs are never deleted — superseded ones are marked `Superseded by ADR-XXX`
- Don't duplicate content between ARCHITECTURE.md and ADRs — ADRs have the full rationale, ARCHITECTURE.md has the concise reference
- Use the same terminology as existing docs (e.g., "feature slice" not "module", "handler" not "service")
- Include code examples in ARCHITECTURE.md sections

---

## Agent: Test Writer

**Trigger:** "Write tests for X", "Add test coverage", "Test this handler"

**Focus:** Meaningful tests that verify behavior, not implementation.

### Behavior

1. Read the handler/code under test
2. Identify the meaningful test cases:
   - Happy path (success result)
   - Expected failure paths (NotFound, Validation errors)
   - Edge cases (empty input, boundary values)
3. Use the test helpers: `FakeInferenceProvider`, `NullCacheService`, etc.
4. Follow naming: `{Method}_When{Condition}_Should{Expectation}`

### Constraints

- Don't test trivial code (DTOs, record constructors, mapping-only methods)
- Don't test framework behavior (EF Core, ASP.NET routing)
- Use `NSubstitute` for mocking, `Testcontainers` for integration
- Assert on the `Result<T>` — check `IsSuccess`, `Value`, `Error.Code`
- One logical assertion per test (multiple `Assert` calls are fine if testing one concept)

### Template

```csharp
[Fact]
public async Task HandleAsync_WhenEntityNotFound_ShouldReturnNotFoundError()
{
    // Arrange
    var handler = new GetConversationHandler(_dbContext, _logger);
    var query = new GetConversationQuery(Guid.NewGuid(), IncludeLogprobs: false);

    // Act
    var result = await handler.HandleAsync(query, CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Type.Should().Be(ErrorType.NotFound);
}
```

---

## Agent: Planner

**Trigger:** "How should I approach X?", "Plan the implementation of Y", "What order should I build Z?"

**Focus:** Breaking down work into ordered steps aligned with the architecture.

### Behavior

1. Read `PROJECT_PLAN.md` for existing task breakdowns
2. Read `ARCHITECTURE.md` for structural constraints
3. Identify dependencies: what needs to exist before this can be built?
4. Break down into concrete steps (not abstract — "create file X" not "set up infrastructure")
5. Call out risks and decisions the user needs to make
6. Estimate relative complexity (small/medium/large) but never time

### Constraints

- Always start with the domain model — get entities right before building UI
- Backend before frontend (API must exist before client is generated)
- Don't plan more than one phase ahead in detail
- Flag when a task requires a new ADR

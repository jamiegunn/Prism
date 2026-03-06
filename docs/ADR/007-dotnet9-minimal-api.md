# ADR-007: .NET 9 Minimal API over Controllers

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The backend needs an API framework. The two primary options in .NET are MVC Controllers and Minimal APIs. Historically, Minimal APIs lacked features like filters, authorization attributes, and OpenAPI generation — but .NET 9 has closed these gaps.

The platform uses vertical slice architecture (ADR-001) where each feature registers its own endpoints. Controllers impose a class-per-group structure with inheritance and decorators that add ceremony without value in a slice-based codebase.

## Decision

Use **.NET 9 Minimal APIs** with route groups, endpoint filters, and native OpenAPI generation.

Full capability parity with controllers:

| Capability | Minimal API (.NET 9) | How |
|------------|---------------------|-----|
| Middleware | Full pipeline support | `app.UseMiddleware<T>()` |
| Endpoint filters | Equivalent to action filters | `AddEndpointFilter<T>()` on route groups or endpoints |
| Authorization | Full policy-based auth | `.RequireAuthorization("PolicyName")` |
| Rate limiting | Built-in | `.RequireRateLimiting("PolicyName")` |
| Output caching | Built-in | `.CacheOutput("PolicyName")` |
| Validation | Via endpoint filters | FluentValidation + `ValidationFilter<T>` |
| OpenAPI / Swagger | Native (.NET 9) | Built-in OpenAPI document generation |
| Route groups | Equivalent to controller grouping | `app.MapGroup("/api/v1/playground")` |
| Model binding | Explicit parameters | `[FromBody]`, `[FromQuery]`, `[FromRoute]`, `[FromServices]` |
| DI | Full support | Parameters resolved automatically |
| Response types | `TypedResults` | Compile-time safe responses |

Each feature slice registers its route group in its module file:

```csharp
public static class PlaygroundModule
{
    public static IEndpointRouteBuilder MapPlaygroundEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/playground")
            .WithTags("Playground")
            .AddEndpointFilter<ValidationFilter<ChatCompletionRequest>>()
            .RequireAuthorization();

        group.MapPost("/chat", PlaygroundEndpoints.Chat);
        group.MapPost("/chat/stream", PlaygroundEndpoints.ChatStream);
        // ...

        return app;
    }
}
```

## Consequences

### Positive

- Less ceremony: no controller classes, no `[ApiController]`, no `[HttpGet]` decorators
- Route groups align naturally with vertical slices — each feature owns its group
- `TypedResults` gives compile-time safety on response types (controllers use runtime `IActionResult`)
- Native OpenAPI in .NET 9 eliminates the Swashbuckle dependency
- Explicit parameter binding — more visible, less magic than controller model binding
- Slightly faster than controllers (no MVC filter pipeline overhead)

### Negative

- Less familiar to developers coming from MVC background
- Some community libraries and tooling still assume controllers
- Route group registration requires explicit wiring in `Program.cs` (vs. controller auto-discovery)
  - Mitigated: each feature's `MapXxxEndpoints()` is called in a central registration block

### Neutral

- `ValidationFilter<T>` provides the same validation behavior as MVC's `[ApiController]` automatic validation
- OpenAPI metadata is added via `.WithName()`, `.WithSummary()`, `.Produces<T>()` fluent methods

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| MVC Controllers | Familiar, auto-discovery, built-in validation | More ceremony, class-per-group overhead, runtime `IActionResult` | Misaligned with vertical slices, no compile-time response safety |
| FastEndpoints library | Structured Minimal API with REPR pattern | External dependency, its own conventions to learn | Adds a framework on top of what .NET 9 already provides natively |
| Carter library | Minimal API organization with modules | Less active maintenance, unnecessary abstraction | .NET 9 route groups provide the same organization natively |

## References

- See `ARCHITECTURE.md` — "Why .NET 9 Minimal API" section with full capability matrix
- [.NET 9 Minimal API documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview?view=aspnetcore-9.0)

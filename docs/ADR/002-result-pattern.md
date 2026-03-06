# ADR-002: Result Pattern for Error Handling

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

.NET applications traditionally use exceptions for error handling. This creates several problems in a research platform:

- Exceptions are invisible in method signatures — callers don't know what can fail
- Try/catch blocks scatter error handling logic across call sites
- Exceptions are expensive (stack trace capture) and conflate expected failures (validation, not found) with unexpected bugs (null reference, I/O failure)
- In a research tool, many operations have expected failure modes (model not loaded, provider unavailable, invalid parameters) that are not exceptional

## Decision

All application-layer operations return `Result<T>` instead of throwing exceptions. Errors are values, not control flow.

```csharp
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }          // Only valid when IsSuccess
    public Error Error { get; }      // Only valid when !IsSuccess

    // Combinators
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper);
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder);
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure);
}
```

Error types map directly to HTTP responses:

| Error Type | HTTP Status |
|-----------|-------------|
| `Error.Validation(...)` | 400 Bad Request |
| `Error.NotFound(...)` | 404 Not Found |
| `Error.Conflict(...)` | 409 Conflict |
| `Error.Unauthorized(...)` | 401 Unauthorized |
| `Error.Forbidden(...)` | 403 Forbidden |
| `Error.External(...)` | 502 Bad Gateway |
| `Error.Unexpected(...)` | 500 Internal Server Error |

Exceptions are reserved for truly unexpected failures (bugs). These are caught by `GlobalExceptionMiddleware` and logged.

## Consequences

### Positive

- Method signatures explicitly declare that an operation can fail
- Callers are forced to handle both success and failure paths — no silent swallowing
- `Match`/`Map`/`Bind` enable composable pipelines without nested if-checks
- Error types map cleanly to HTTP status codes at the API boundary
- Performance: no stack trace capture for expected failures

### Negative

- More verbose than just returning `T` for operations that rarely fail
- Developers must learn the combinator pattern (`Map`, `Bind`, `Match`)
- Wrapping third-party exceptions into `Result` at infrastructure boundaries requires discipline

### Neutral

- Infrastructure-layer code catches exceptions from external libraries (EF Core, HTTP clients) and wraps them in `Result.Failure`
- Domain-layer code never throws — it returns `Result`

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Exceptions everywhere | Familiar, less boilerplate | Invisible failure modes, expensive, poor composition | Expected failures (model not found, invalid params) are common in research workflows |
| Nullable returns + error out params | Simple | Error details are stringly-typed, easy to ignore | Loses structured error information |
| FluentResults / ErrorOr libraries | Pre-built, community supported | External dependency for a core pattern, less control | Want full control over error taxonomy and HTTP mapping |

## References

- See `ARCHITECTURE.md` — Result Pattern section for full implementation
- Railway-oriented programming (Scott Wlaschin)

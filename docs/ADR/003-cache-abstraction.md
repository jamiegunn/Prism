# ADR-003: Cache Provider Abstraction

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The platform needs caching for inference results, tokenizer outputs, and provider metadata. Starting with in-memory caching is appropriate for a local-first tool, but as the platform scales or moves to multi-instance deployment, a distributed cache (Redis) will be needed.

Without an abstraction, cache calls would be scattered across feature code using `IMemoryCache` or `IDistributedCache` directly, making a provider swap a rewrite.

## Decision

Introduce `ICacheService` as the sole caching interface consumed by feature code:

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        CacheOptions? options = null, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
```

Implementations:

| Provider | Config Value | Use Case |
|----------|-------------|----------|
| `InMemoryCacheService` | `"InMemory"` | Default — local dev, single instance |
| `RedisCacheService` | `"Redis"` | Distributed, multi-instance |
| `NullCacheService` | `"None"` | Testing, disabled cache |

Provider is selected via configuration: `"Cache:Provider": "InMemory"`.

## Consequences

### Positive

- Feature code depends only on `ICacheService` — zero coupling to cache implementation
- Swap from in-memory to Redis by changing one config value
- `NullCacheService` simplifies testing (no mock setup needed)
- `GetOrSetAsync` prevents cache stampede with factory pattern
- `RemoveByPrefixAsync` enables bulk invalidation (e.g., all entries for a model)

### Negative

- `RemoveByPrefixAsync` is trivial with in-memory but requires key scanning with Redis — performance consideration for large key spaces
- Serialization overhead with Redis (in-memory stores references directly)
- Extra layer of indirection for what starts as a simple `IMemoryCache` call

### Neutral

- `CacheOptions` carries `AbsoluteExpiration` and `SlidingExpiration`
- Feature code uses key conventions: `{feature}:{entity}:{id}` (e.g., `playground:result:abc123`)

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Use `IDistributedCache` directly | Built into .NET, well-known | Byte-array API is clunky, no `GetOrSet`, no typed generics | Poor developer experience for typed domain objects |
| Use `IMemoryCache` now, swap later | Zero abstraction overhead | "Swap later" means rewriting every call site | Violates provider abstraction principle |
| HybridCache (.NET 9) | New built-in abstraction | Still relatively new, less control over provider swap semantics | May adopt later if it matures and fits our needs |

## References

- See `ARCHITECTURE.md` — Cache Abstraction section
- ADR-001 — features register their own cache key prefixes within their slice

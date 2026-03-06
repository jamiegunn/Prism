# ADR-005: Authentication Provider Abstraction

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The AI Research Workbench starts as a local-first, single-user tool where authentication adds friction without value. However, future scenarios include:

- Multi-user lab environments sharing a single deployment
- Enterprise deployments requiring Entra ID (Azure AD) or OIDC federation
- API key authentication for programmatic access

Hard-coding "no auth" throughout the codebase, or directly coupling to a specific auth provider, means a painful rewrite when auth requirements change.

## Decision

Introduce `IAuthProvider` and `ICurrentUser` abstractions:

```csharp
public interface IAuthProvider
{
    string ProviderName { get; }
    Task<Result<AuthResult>> AuthenticateAsync(AuthRequest request, CancellationToken ct);
    Task<Result<UserInfo>> ValidateTokenAsync(string token, CancellationToken ct);
    Task<Result<AuthResult>> RefreshTokenAsync(string refreshToken, CancellationToken ct);
    Task<Result> RevokeTokenAsync(string token, CancellationToken ct);
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? DisplayName { get; }
    string? Email { get; }
    IReadOnlySet<string> Roles { get; }
    bool IsInRole(string role);
}
```

Phase 1 uses `NoAuthProvider`, which:
- `AuthenticateAsync` always returns success with a fixed local user token
- `ValidateTokenAsync` always returns a valid `UserInfo` for the local user
- `ICurrentUser` is always authenticated as the "local-researcher" user with an "Admin" role

Implementations:

| Provider | Config Value | Use Case |
|----------|-------------|----------|
| `NoAuthProvider` | `"None"` | Default — local dev, single user, no login required |
| `LocalJwtProvider` | `"LocalJwt"` | Local JWT with username/password, multi-user |
| `EntraProvider` | `"Entra"` | Microsoft Entra ID (Azure AD) federation |
| `OidcProvider` | `"OIDC"` | Generic OpenID Connect provider |

Provider is selected via configuration: `"Auth:Provider": "None"`.

## Consequences

### Positive

- Feature code accesses `ICurrentUser` (scoped service) — never checks auth directly
- `NoAuthProvider` means zero friction in Phase 1 — no login screens, no token management
- Swap to Entra/OIDC by changing config and adding provider settings
- Authorization policies reference `ICurrentUser.Roles` — work identically regardless of auth provider
- All operations return `Result<T>` — consistent with platform error handling (ADR-002)

### Negative

- `NoAuthProvider` means no audit trail of who did what in Phase 1 (everything is "local-researcher")
- Auth abstraction adds indirection for what is initially a no-op
- Each new auth provider requires implementing the full `IAuthProvider` interface

### Neutral

- `AuthRequest` supports multiple credential types via a discriminated union: `UsernamePassword`, `RefreshToken`, `ExternalToken`
- `AuthResult` contains: `AccessToken`, `RefreshToken`, `ExpiresAt`, `UserInfo`
- Middleware extracts the token from `Authorization: Bearer` header and populates `ICurrentUser` via `IAuthProvider.ValidateTokenAsync`

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| No auth, add later | Zero overhead now | Every feature would need retrofit when auth is added | "Add later" becomes a rewrite of every endpoint |
| ASP.NET Identity directly | Full-featured, built-in | Tightly coupled to EF Core Identity tables, hard to swap to Entra | Locks us into one identity store |
| Auth0 / third-party from start | Enterprise-ready | External dependency, cost, overkill for local tool | Premature for Phase 1 |

## References

- See `ARCHITECTURE.md` — Authentication Abstraction section
- `NoAuthProvider` is defined in `Common/Auth/NoAuthProvider.cs`

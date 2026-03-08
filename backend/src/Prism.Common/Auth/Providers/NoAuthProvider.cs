using Prism.Common.Results;

namespace Prism.Common.Auth.Providers;

/// <summary>
/// A no-op authentication provider that always succeeds and returns a default "local-user" identity.
/// Used in development mode and single-user deployments where authentication is not required.
/// </summary>
public sealed class NoAuthProvider : IAuthProvider
{
    private static readonly UserInfo DefaultUser = new(
        UserId: "local-user",
        DisplayName: "Local User",
        Email: "local@prism.dev",
        Roles: new[] { "admin", "user" });

    private static readonly AuthResult DefaultAuthResult = new(
        AccessToken: "no-auth-token",
        RefreshToken: null,
        ExpiresAt: DateTime.MaxValue,
        User: DefaultUser);

    /// <summary>
    /// Always returns a successful authentication result with the default local user.
    /// </summary>
    /// <param name="request">The authentication request (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result containing the default auth tokens and local user info.</returns>
    public Task<Result<AuthResult>> AuthenticateAsync(AuthRequest request, CancellationToken ct) =>
        Task.FromResult<Result<AuthResult>>(DefaultAuthResult);

    /// <summary>
    /// Always returns a successful validation result with the default local user.
    /// </summary>
    /// <param name="token">The access token to validate (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result containing the default local user info.</returns>
    public Task<Result<UserInfo>> ValidateTokenAsync(string token, CancellationToken ct) =>
        Task.FromResult<Result<UserInfo>>(DefaultUser);

    /// <summary>
    /// Always returns a successful refresh result with the default local user.
    /// </summary>
    /// <param name="refreshToken">The refresh token (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result containing the default auth tokens and local user info.</returns>
    public Task<Result<AuthResult>> RefreshTokenAsync(string refreshToken, CancellationToken ct) =>
        Task.FromResult<Result<AuthResult>>(DefaultAuthResult);

    /// <summary>
    /// Always returns a successful revocation result (no-op).
    /// </summary>
    /// <param name="token">The token to revoke (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result indicating the token was revoked.</returns>
    public Task<Result> RevokeTokenAsync(string token, CancellationToken ct) =>
        Task.FromResult(Result.Success());
}

using Prism.Common.Results;

namespace Prism.Common.Auth;

/// <summary>
/// Defines the authentication provider contract for the application.
/// Implementations handle different authentication strategies (NoAuth, local JWT, Entra, OIDC).
/// </summary>
public interface IAuthProvider
{
    /// <summary>
    /// Authenticates a user using the provided credentials or token.
    /// </summary>
    /// <param name="request">The authentication request containing credentials.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the authentication tokens and user info on success.</returns>
    Task<Result<AuthResult>> AuthenticateAsync(AuthRequest request, CancellationToken ct);

    /// <summary>
    /// Validates an access token and returns the associated user information.
    /// </summary>
    /// <param name="token">The access token to validate.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the user info if the token is valid.</returns>
    Task<Result<UserInfo>> ValidateTokenAsync(string token, CancellationToken ct);

    /// <summary>
    /// Exchanges a refresh token for a new access token and optionally a new refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to exchange.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the new authentication tokens on success.</returns>
    Task<Result<AuthResult>> RefreshTokenAsync(string refreshToken, CancellationToken ct);

    /// <summary>
    /// Revokes an access or refresh token, preventing further use.
    /// </summary>
    /// <param name="token">The token to revoke.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure of the revocation.</returns>
    Task<Result> RevokeTokenAsync(string token, CancellationToken ct);
}

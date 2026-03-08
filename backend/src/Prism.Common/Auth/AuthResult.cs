namespace Prism.Common.Auth;

/// <summary>
/// Represents the result of a successful authentication operation.
/// </summary>
/// <param name="AccessToken">The JWT access token for API authorization.</param>
/// <param name="RefreshToken">The optional refresh token for obtaining new access tokens.</param>
/// <param name="ExpiresAt">The UTC timestamp when the access token expires.</param>
/// <param name="User">The authenticated user's information.</param>
public sealed record AuthResult(string AccessToken, string? RefreshToken, DateTime ExpiresAt, UserInfo User);

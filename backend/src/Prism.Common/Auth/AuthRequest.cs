namespace Prism.Common.Auth;

/// <summary>
/// Represents an authentication request containing credentials or a token for authentication.
/// </summary>
/// <param name="Username">The username for credential-based authentication. Null for token-based flows.</param>
/// <param name="Password">The password for credential-based authentication. Null for token-based flows.</param>
/// <param name="Token">A token for token-based authentication or refresh flows. Null for credential-based flows.</param>
public sealed record AuthRequest(string? Username, string? Password, string? Token);

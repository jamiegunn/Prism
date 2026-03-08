namespace Prism.Common.Auth;

/// <summary>
/// Represents canonical user information returned from authentication.
/// </summary>
/// <param name="UserId">The unique identifier for the user.</param>
/// <param name="DisplayName">The display name of the user.</param>
/// <param name="Email">The email address of the user, if available.</param>
/// <param name="Roles">The collection of roles assigned to the user.</param>
public sealed record UserInfo(string UserId, string DisplayName, string? Email, IReadOnlyList<string> Roles);

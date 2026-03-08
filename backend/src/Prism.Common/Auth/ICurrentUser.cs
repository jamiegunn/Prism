namespace Prism.Common.Auth;

/// <summary>
/// Provides information about the currently authenticated user for the current request scope.
/// Resolved as a scoped service and populated by authentication middleware.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Gets a value indicating whether the current request is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the unique identifier of the authenticated user, or null if not authenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the display name of the authenticated user, or null if not authenticated.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Gets the email address of the authenticated user, or null if not available.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the collection of roles assigned to the authenticated user.
    /// Returns an empty collection if not authenticated.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Determines whether the current user is in the specified role.
    /// </summary>
    /// <param name="role">The role name to check.</param>
    /// <returns>True if the user is in the specified role; otherwise, false.</returns>
    bool IsInRole(string role);
}

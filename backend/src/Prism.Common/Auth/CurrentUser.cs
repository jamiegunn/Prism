namespace Prism.Common.Auth;

/// <summary>
/// Mutable implementation of <see cref="ICurrentUser"/> that is populated by authentication middleware.
/// Registered as a scoped service so each request gets its own instance.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    /// <summary>
    /// Gets or sets a value indicating whether the current request is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the authenticated user.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the authenticated user.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the authenticated user.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the collection of roles assigned to the authenticated user.
    /// </summary>
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Determines whether the current user is in the specified role.
    /// </summary>
    /// <param name="role">The role name to check.</param>
    /// <returns>True if the user is authenticated and in the specified role; otherwise, false.</returns>
    public bool IsInRole(string role) =>
        IsAuthenticated && Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Populates this instance from a <see cref="UserInfo"/> record.
    /// Called by authentication middleware after successful token validation.
    /// </summary>
    /// <param name="userInfo">The user information to populate from.</param>
    public void SetFromUserInfo(UserInfo userInfo)
    {
        IsAuthenticated = true;
        UserId = userInfo.UserId;
        DisplayName = userInfo.DisplayName;
        Email = userInfo.Email;
        Roles = userInfo.Roles;
    }
}

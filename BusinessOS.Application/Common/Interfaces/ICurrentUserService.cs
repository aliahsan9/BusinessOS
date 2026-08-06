namespace BusinessOS.Application.Common.Interfaces;

/// <summary>
/// Provides access to the claims and identity details of the currently authenticated HTTP user.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Gets the unique identifier of the authenticated user, or <c>null</c> if unauthenticated.</summary>
    string? UserId { get; }

    /// <summary>Gets the email address of the authenticated user.</summary>
    string? Email { get; }

    /// <summary>Gets the tenant ID associated with the current user session.</summary>
    Guid? TenantId { get; }

    /// <summary>Gets the list of role names assigned to the current user.</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>Gets the list of permission keys granted to the current user.</summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// Checks whether the user holds a specific permission code.
    /// </summary>
    /// <param name="permissionCode">The permission key string to evaluate.</param>
    /// <returns><c>true</c> if the user holds the permission; otherwise, <c>false</c>.</returns>
    bool HasPermission(string permissionCode);
}

using Microsoft.AspNetCore.Authorization;

namespace DotNetCloud.Core.Auth.Authorization;

/// <summary>
/// Defines the authorization policy names used throughout DotNetCloud.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Policy name that requires an authenticated user with the <c>admin</c> permission.
    /// </summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>
    /// Policy name that requires any authenticated user.
    /// </summary>
    public const string RequireAuthenticated = "RequireAuthenticated";

    /// <summary>
    /// Policy name that requires the <c>files.read</c> permission.
    /// </summary>
    public const string RequireFilesRead = "RequireFilesRead";

    /// <summary>
    /// Policy name that requires the <c>files.write</c> permission.
    /// </summary>
    public const string RequireFilesWrite = "RequireFilesWrite";

    /// <summary>
    /// Policy name that requires the <c>bookmarks.read</c> permission.
    /// </summary>
    public const string RequireBookmarksRead = "RequireBookmarksRead";

    /// <summary>
    /// Policy name that requires the <c>bookmarks.write</c> permission.
    /// </summary>
    public const string RequireBookmarksWrite = "RequireBookmarksWrite";

    /// <summary>
    /// Registers all DotNetCloud authorization policies into the provided options.
    /// </summary>
    /// <param name="options">The <see cref="AuthorizationOptions"/> to configure.</param>
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(RequireAuthenticated,
            policy => policy.RequireAuthenticatedUser());

        options.AddPolicy(RequireAdmin,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement("admin")));

        options.AddPolicy(RequireFilesRead,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement("files.read")));

        options.AddPolicy(RequireFilesWrite,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement("files.write")));

        options.AddPolicy(RequireBookmarksRead,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement("bookmarks.read")));

        options.AddPolicy(RequireBookmarksWrite,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement("bookmarks.write")));
    }
}

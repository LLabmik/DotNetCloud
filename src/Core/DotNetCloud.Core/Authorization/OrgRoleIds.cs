namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Well-known organization role GUIDs.
/// These are seeded into the Permissions.Role table and referenced
/// by OrganizationMember.RoleIds at runtime.
/// </summary>
public static class OrgRoleIds
{
    public static readonly Guid OrgAdmin   = Guid.Parse("a1b2c3d4-0002-4000-8000-000000000001");
    public static readonly Guid OrgManager = Guid.Parse("a1b2c3d4-0001-4000-8000-000000000001");
    public static readonly Guid OrgMember  = Guid.Parse("a1b2c3d4-0003-4000-8000-000000000003");

    /// <summary>
    /// Returns a human-readable name for a well-known org role GUID.
    /// </summary>
    public static string GetName(Guid roleId)
    {
        if (roleId == OrgAdmin)   return "Org Admin";
        if (roleId == OrgManager) return "Org Manager";
        if (roleId == OrgMember)  return "Org Member";
        return "Unknown";
    }

    /// <summary>
    /// All well-known org role GUIDs in hierarchy order (highest first).
    /// </summary>
    public static readonly IReadOnlyList<Guid> All = [OrgAdmin, OrgManager, OrgMember];
}

/// <summary>
/// Well-known system role names for Identity-based roles.
/// </summary>
public static class SystemRoleNames
{
    public const string Administrator = "Administrator";
}

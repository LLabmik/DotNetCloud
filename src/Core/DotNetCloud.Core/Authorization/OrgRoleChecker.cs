namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Stateless helper for checking org role membership against a set of role GUIDs.
/// </summary>
public static class OrgRoleChecker
{
    public static bool HasAdminRole(IEnumerable<Guid> roleIds) =>
        roleIds.Contains(OrgRoleIds.OrgAdmin);

    public static bool HasManagerOrAboveRole(IEnumerable<Guid> roleIds) =>
        roleIds.Contains(OrgRoleIds.OrgManager) || roleIds.Contains(OrgRoleIds.OrgAdmin);

    public static bool HasMemberOrAboveRole(IEnumerable<Guid> roleIds) =>
        roleIds.Contains(OrgRoleIds.OrgMember)
        || roleIds.Contains(OrgRoleIds.OrgManager)
        || roleIds.Contains(OrgRoleIds.OrgAdmin);

    /// <summary>
    /// Returns the highest org role GUID present, or null if none.
    /// </summary>
    public static Guid? GetHighestRole(IEnumerable<Guid> roleIds)
    {
        if (roleIds.Contains(OrgRoleIds.OrgAdmin))   return OrgRoleIds.OrgAdmin;
        if (roleIds.Contains(OrgRoleIds.OrgManager)) return OrgRoleIds.OrgManager;
        if (roleIds.Contains(OrgRoleIds.OrgMember))  return OrgRoleIds.OrgMember;
        return null;
    }
}

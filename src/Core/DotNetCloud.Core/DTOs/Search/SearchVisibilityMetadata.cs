namespace DotNetCloud.Core.DTOs.Search;

/// <summary>
/// Shared metadata keys used for search visibility and mounted Files navigation.
/// </summary>
public static class SearchVisibilityMetadata
{
    /// <summary>Metadata key describing how a document's visibility should be interpreted.</summary>
    public const string VisibilityScopeKey = "VisibilityScope";

    /// <summary>Metadata value indicating the document is visible to matching group members.</summary>
    public const string VisibilityScopeGroupMembers = "GroupMembers";

    /// <summary>Metadata key containing the pipe-delimited group scope key.</summary>
    public const string GroupScopeKey = "GroupScopeKey";

    /// <summary>Metadata key containing the admin shared-folder identifier.</summary>
    public const string SharedFolderIdKey = "SharedFolderId";

    /// <summary>Metadata key containing the mounted relative path within the shared folder.</summary>
    public const string RelativePathKey = "RelativePath";

    /// <summary>Metadata key containing the mounted source kind.</summary>
    public const string VirtualSourceKindKey = "VirtualSourceKind";

    /// <summary>Builds the normalized pipe-delimited group scope key for metadata filtering.</summary>
    public static string BuildGroupScopeKey(IEnumerable<Guid> groupIds)
    {
        var normalized = groupIds
            .Distinct()
            .OrderBy(groupId => groupId)
            .Select(groupId => groupId.ToString("D"))
            .ToArray();

        return normalized.Length == 0
            ? string.Empty
            : $"|{string.Join('|', normalized)}|";
    }
}
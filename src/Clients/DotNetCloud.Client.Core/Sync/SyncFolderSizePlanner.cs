using DotNetCloud.Client.Core.Api;

namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Determines which folders under a sync root exceed a folder size limit (recursive total size),
/// maximizing the set of included folders. The algorithm drills down into subfolders that
/// individually exceed the limit and excludes only the deepest over-limit folders; a folder
/// whose own aggregate (files + many small children) exceeds the limit is itself excluded.
/// </summary>
public static class SyncFolderSizePlanner
{
    /// <summary>
    /// Returns the folder relative paths (forward slashes, no leading slash) that should be
    /// excluded because their recursive total size exceeds <paramref name="limitBytes"/>.
    /// The sync root itself is never excluded.
    /// </summary>
    public static IReadOnlyList<string> PlanExclusions(SyncTreeNodeResponse tree, long limitBytes)
    {
        var exclusions = new List<string>();
        if (IsFolder(tree.NodeType))
        {
            ComputeSizes(tree, "", limitBytes, exclusions);
        }

        return exclusions;
    }

    /// <summary>Computes the recursive total size of <paramref name="node"/>'s subtree and
    /// records deepest over-limit folders in <paramref name="exclusions"/>.</summary>
    private static long ComputeSizes(SyncTreeNodeResponse node, string relativePath, long limitBytes, List<string> exclusions)
    {
        long total = node.Size;
        var hasOverLimitChildFolder = false;

        foreach (var child in node.Children)
        {
            var childRel = string.IsNullOrEmpty(relativePath)
                ? child.Name
                : $"{relativePath}/{child.Name}";
            var childTotal = ComputeSizes(child, childRel, limitBytes, exclusions);
            total += childTotal;

            if (IsFolder(child.NodeType) && childTotal > limitBytes)
            {
                hasOverLimitChildFolder = true;
            }
        }

        // Only folders are exclusion candidates. A folder is excluded when its recursive size
        // exceeds the limit AND no child folder individually exceeds it (drill-down ended: the
        // excess comes from this folder's own files or the aggregate of many small children).
        if (IsFolder(node.NodeType)
            && !string.IsNullOrEmpty(relativePath)
            && total > limitBytes
            && !hasOverLimitChildFolder)
        {
            exclusions.Add(relativePath);
        }

        return total;
    }

    private static bool IsFolder(string? nodeType) =>
        string.Equals(nodeType, "Folder", StringComparison.OrdinalIgnoreCase)
        || string.Equals(nodeType, "Directory", StringComparison.OrdinalIgnoreCase);
}

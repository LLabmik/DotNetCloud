namespace DotNetCloud.Modules.Files;

/// <summary>
/// Ordering helpers for the synthetic (virtual) nodes surfaced by the Files module.
/// </summary>
public static class VirtualNodeOrdering
{
    /// <summary>
    /// Orders nodes so the <c>_DotNetCloud</c> virtual root is first, leaving the relative order of
    /// all other nodes untouched (LINQ ordering is stable). Callers can chain
    /// <see cref="System.Linq.Enumerable.ThenBy{TSource, TKey}(System.Linq.IOrderedEnumerable{TSource}, System.Func{TSource, TKey})"/>
    /// to apply their own sort within each group.
    /// </summary>
    /// <typeparam name="T">Node type.</typeparam>
    /// <param name="nodes">Nodes to order.</param>
    /// <param name="name">Selector for the node display name.</param>
    /// <param name="virtualSourceKind">Selector for the node's synthetic source kind.</param>
    /// <returns>The nodes with the <c>_DotNetCloud</c> virtual root pinned first.</returns>
    public static IOrderedEnumerable<T> PinDotNetCloudRootFirst<T>(
        this IEnumerable<T> nodes,
        Func<T, string?> name,
        Func<T, string?> virtualSourceKind)
        => nodes.OrderByDescending(n => VirtualNodeSourceKinds.IsDotNetCloudRoot(name(n), virtualSourceKind(n)));
}

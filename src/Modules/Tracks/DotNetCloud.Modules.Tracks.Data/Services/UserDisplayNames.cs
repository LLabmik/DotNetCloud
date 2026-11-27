using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Resolves user display names from the core user directory for payloads that store only a user id.
/// </summary>
/// <remarks>
/// <para>
/// Tracks stores ids everywhere — an assignment row, a review-session participant, an analytics
/// rollup — but the name lives in the core's user directory, which a sandboxed module can only
/// reach through the <see cref="IUserDirectory"/> capability. Without this resolution payloads
/// shipped <c>displayName: null</c> (the framed UI then fell back to "User"/a short id, and an
/// assignee avatar rendered as "?") or, worse, put the raw GUID in the name field.
/// </para>
/// <para>
/// Every lookup is batched: one directory call per payload, and only for ids whose name is still
/// blank. A missing capability (unit tests, a host without a core endpoint) or a directory that
/// fails leaves the name as the caller supplied it, so the UI keeps its own fallback and the
/// request never fails because of a name.
/// </para>
/// </remarks>
public static class UserDisplayNames
{
    private static readonly IReadOnlyDictionary<Guid, string> EmptyNames =
        new Dictionary<Guid, string>();

    /// <summary>
    /// Resolves display names for a batch of user ids.
    /// </summary>
    /// <param name="userIds">The ids to resolve; duplicates and <see cref="Guid.Empty"/> are ignored.</param>
    /// <param name="userDirectory">The capability, or <c>null</c> when it is not available.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Id-to-name for the ids the directory knows; empty when it is unavailable.</returns>
    public static async Task<IReadOnlyDictionary<Guid, string>> LookupAsync(
        IEnumerable<Guid> userIds,
        IUserDirectory? userDirectory,
        CancellationToken ct = default)
    {
        if (userDirectory is null)
            return EmptyNames;

        var unresolved = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (unresolved.Count == 0)
            return EmptyNames;

        try
        {
            return await userDirectory.GetDisplayNamesAsync(unresolved, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // An unreachable directory must not fail the payload: the name stays as supplied and
            // the caller falls back to the id.
            return EmptyNames;
        }
    }

    /// <summary>
    /// Fills the blank display names of one work item's assignments.
    /// </summary>
    /// <param name="assignments">The assignments as mapped from the data model.</param>
    /// <param name="userDirectory">The capability, or <c>null</c> when it is not available.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The same assignments, with blank display names filled where a name is known.</returns>
    public static async Task<List<WorkItemAssignmentDto>> ResolveAssignmentsAsync(
        IEnumerable<WorkItemAssignmentDto> assignments,
        IUserDirectory? userDirectory,
        CancellationToken ct = default)
    {
        var list = assignments.ToList();

        var names = await LookupAsync(
            list.Where(a => string.IsNullOrWhiteSpace(a.DisplayName)).Select(a => a.UserId),
            userDirectory,
            ct);

        if (names.Count == 0)
            return list;

        return list
            .Select(a => string.IsNullOrWhiteSpace(a.DisplayName) && names.TryGetValue(a.UserId, out var name)
                ? a with { DisplayName = name }
                : a)
            .ToList();
    }

    /// <summary>
    /// Fills the blank assignment display names of every work item in a payload.
    /// </summary>
    /// <param name="workItems">The work items as mapped from the data model.</param>
    /// <param name="userDirectory">The capability, or <c>null</c> when it is not available.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The same work items, with blank assignment display names filled where known.</returns>
    public static async Task<List<WorkItemDto>> ResolveWorkItemsAsync(
        IEnumerable<WorkItemDto> workItems,
        IUserDirectory? userDirectory,
        CancellationToken ct = default)
    {
        var list = workItems.ToList();

        var names = await LookupAsync(
            list.SelectMany(i => i.Assignments)
                .Where(a => string.IsNullOrWhiteSpace(a.DisplayName))
                .Select(a => a.UserId),
            userDirectory,
            ct);

        if (names.Count == 0)
            return list;

        return list
            .Select(item => item.Assignments.Count == 0
                ? item
                : item with
                {
                    Assignments = item.Assignments
                        .Select(a => string.IsNullOrWhiteSpace(a.DisplayName) && names.TryGetValue(a.UserId, out var name)
                            ? a with { DisplayName = name }
                            : a)
                        .ToList()
                })
            .ToList();
    }
}

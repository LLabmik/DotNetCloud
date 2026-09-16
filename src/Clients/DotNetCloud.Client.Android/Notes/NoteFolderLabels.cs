using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Notes;

/// <summary>
/// Resolves the display label for the folder a note belongs to.
/// </summary>
/// <remarks>
/// Note folders belong to their owner, so a note shared with the caller can live in a folder that
/// is not in the caller's own folder list. In that case the folder cannot be named and the
/// fallback label is used instead — the same behaviour the Blazor sidebar has.
/// </remarks>
public static class NoteFolderLabels
{
    /// <summary>Label shown when a note's folder is not in the caller's own folder list.</summary>
    public const string SharedFolderFallback = "Shared folder";

    /// <summary>
    /// Returns the folder name for <paramref name="folderId"/>, the shared-folder fallback when the
    /// folder is not in <paramref name="folders"/>, or <c>null</c> when the note is unfiled.
    /// </summary>
    /// <param name="folderId">Folder the note is filed under, or <c>null</c> when unfiled.</param>
    /// <param name="folders">Folders visible to the caller (may be <c>null</c> or empty).</param>
    public static string? Resolve(Guid? folderId, IReadOnlyList<NoteFolderDto>? folders)
    {
        if (folderId is not { } id)
            return null;

        var name = folders?.FirstOrDefault(f => f.Id == id)?.Name;
        return string.IsNullOrWhiteSpace(name) ? SharedFolderFallback : name;
    }
}

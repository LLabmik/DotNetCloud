using DotNetCloud.Modules.Bookmarks.Models;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Bookmarks.UI;

/// <summary>Code-behind for the <see cref="BookmarkGrid"/> component.</summary>
public partial class BookmarkGrid : ComponentBase
{
    [Parameter, EditorRequired] public required IReadOnlyList<BookmarkItem> Items { get; set; }
    [Parameter] public Func<BookmarkItem, Task> OnSelect { get; set; } = _ => Task.CompletedTask;
    [Parameter] public Func<BookmarkItem, Task> OnToggleFavorite { get; set; } = _ => Task.CompletedTask;
    [Parameter] public Func<BookmarkItem, Task> OnDelete { get; set; } = _ => Task.CompletedTask;
    [Parameter] public Func<BookmarkItem, Task> OnFetchPreview { get; set; } = _ => Task.CompletedTask;
}

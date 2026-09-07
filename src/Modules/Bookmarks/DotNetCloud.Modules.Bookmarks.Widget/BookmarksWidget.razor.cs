using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Bookmarks.Widget;

/// <summary>
/// Home-page widget for the Bookmarks module: the five most recently created bookmarks
/// for the signed-in user.
/// </summary>
public partial class BookmarksWidget : ComponentBase
{
    [Inject] private IBookmarksApiClient ApiClient { get; set; } = default!;

    private readonly List<BookmarkItemDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No bookmarks yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var bookmarks = await ApiClient.GetRecentBookmarksAsync(5);
            _items.AddRange(bookmarks);
        }
        catch (Exception)
        {
            _error = "Unable to load bookmarks.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Returns the display text for a bookmark row: the user title when present,
    /// otherwise the bookmark's URL.
    /// </summary>
    private static string DisplayTitle(BookmarkItemDto item)
        => string.IsNullOrWhiteSpace(item.Title) ? item.Url : item.Title;
}

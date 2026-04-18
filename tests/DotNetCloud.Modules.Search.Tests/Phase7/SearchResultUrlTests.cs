using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Tests.Phase7;

/// <summary>
/// Tests for search result URL generation and module helper methods used across
/// Phase 7 Blazor UI components (GlobalSearchBar, SearchResults, SearchResultCard).
/// </summary>
[TestClass]
public class SearchResultUrlTests
{
    #region GetResultUrl — Module deep-link routing

    [TestMethod]
    public void GetResultUrl_FilesModule_ReturnsFileIdUrl()
    {
        var item = CreateItem("files", "FileNode", "abc-123");
        Assert.AreEqual("/apps/files?fileId=abc-123", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_NotesModule_ReturnsNoteIdUrl()
    {
        var item = CreateItem("notes", "Note", "note-456");
        Assert.AreEqual("/apps/notes?noteId=note-456", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_ChatModule_WithChannelId_ReturnsChannelAndMessageUrl()
    {
        var item = CreateItem("chat", "Message", "msg-789",
            new Dictionary<string, string> { ["ChannelId"] = "ch-001" });
        Assert.AreEqual("/apps/chat?channelId=ch-001&messageId=msg-789", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_ChatModule_WithoutChannelId_ReturnsMessageOnlyUrl()
    {
        var item = CreateItem("chat", "Message", "msg-789");
        Assert.AreEqual("/apps/chat?messageId=msg-789", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_ContactsModule_ReturnsContactIdUrl()
    {
        var item = CreateItem("contacts", "Contact", "c-010");
        Assert.AreEqual("/apps/contacts?contactId=c-010", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_CalendarModule_ReturnsEventIdUrl()
    {
        var item = CreateItem("calendar", "CalendarEvent", "evt-020");
        Assert.AreEqual("/apps/calendar?eventId=evt-020", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_PhotosModule_ReturnsPhotoIdUrl()
    {
        var item = CreateItem("photos", "Photo", "ph-030");
        Assert.AreEqual("/apps/photos?photoId=ph-030", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_MusicModule_ReturnsTrackIdUrl()
    {
        var item = CreateItem("music", "Track", "trk-040");
        Assert.AreEqual("/apps/music?trackId=trk-040", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_VideoModule_ReturnsVideoIdUrl()
    {
        var item = CreateItem("video", "Video", "vid-050");
        Assert.AreEqual("/apps/video?videoId=vid-050", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_TracksModule_ReturnsCardIdUrl()
    {
        var item = CreateItem("tracks", "Card", "card-060");
        Assert.AreEqual("/apps/tracks?cardId=card-060", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_AiModule_ReturnsConversationIdUrl()
    {
        var item = CreateItem("ai", "ConversationMessage", "conv-070");
        Assert.AreEqual("/apps/ai?conversationId=conv-070", SearchUrlHelper.GetResultUrl(item));
    }

    [TestMethod]
    public void GetResultUrl_UnknownModule_ReturnsTitleBasedSearchUrl()
    {
        var item = CreateItem("unknown-module", "SomeType", "id-100");
        Assert.AreEqual("/search?q=Test Title", SearchUrlHelper.GetResultUrl(item));
    }

    #endregion

    #region GetModuleIcon — Emoji mapping

    [TestMethod]
    [DataRow("files", "📁")]
    [DataRow("notes", "📝")]
    [DataRow("chat", "💬")]
    [DataRow("contacts", "👤")]
    [DataRow("calendar", "📅")]
    [DataRow("photos", "📷")]
    [DataRow("music", "🎵")]
    [DataRow("video", "🎬")]
    [DataRow("tracks", "📋")]
    [DataRow("ai", "🤖")]
    [DataRow("unknown", "🔍")]
    public void GetModuleIcon_ReturnsCorrectIcon(string moduleId, string expectedIcon)
    {
        Assert.AreEqual(expectedIcon, SearchUrlHelper.GetModuleIcon(moduleId));
    }

    #endregion

    #region FormatModuleName — Display name mapping

    [TestMethod]
    [DataRow("files", "Files")]
    [DataRow("notes", "Notes")]
    [DataRow("chat", "Chat")]
    [DataRow("contacts", "Contacts")]
    [DataRow("calendar", "Calendar")]
    [DataRow("photos", "Photos")]
    [DataRow("music", "Music")]
    [DataRow("video", "Video")]
    [DataRow("tracks", "Tracks")]
    [DataRow("ai", "AI")]
    [DataRow("custom-module", "custom-module")]
    public void FormatModuleName_ReturnsCorrectName(string moduleId, string expected)
    {
        Assert.AreEqual(expected, SearchUrlHelper.FormatModuleName(moduleId));
    }

    #endregion

    private static SearchResultItem CreateItem(
        string moduleId, string entityType, string entityId,
        Dictionary<string, string>? metadata = null)
    {
        return new SearchResultItem
        {
            ModuleId = moduleId,
            EntityType = entityType,
            EntityId = entityId,
            Title = "Test Title",
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }
}

/// <summary>
/// Extracts the shared helper methods from the Blazor components for testability.
/// These are the same static methods used in GlobalSearchBar.razor, SearchResults.razor,
/// and SearchResultCard.razor.
/// </summary>
public static class SearchUrlHelper
{
    public static string GetResultUrl(SearchResultItem item) => item.ModuleId switch
    {
        "files" => $"/apps/files?fileId={item.EntityId}",
        "notes" => $"/apps/notes?noteId={item.EntityId}",
        "chat" => item.Metadata.TryGetValue("ChannelId", out var channelId)
            ? $"/apps/chat?channelId={channelId}&messageId={item.EntityId}"
            : $"/apps/chat?messageId={item.EntityId}",
        "contacts" => $"/apps/contacts?contactId={item.EntityId}",
        "calendar" => $"/apps/calendar?eventId={item.EntityId}",
        "photos" => $"/apps/photos?photoId={item.EntityId}",
        "music" => $"/apps/music?trackId={item.EntityId}",
        "video" => $"/apps/video?videoId={item.EntityId}",
        "tracks" => $"/apps/tracks?cardId={item.EntityId}",
        "ai" => $"/apps/ai?conversationId={item.EntityId}",
        _ => $"/search?q={item.Title}"
    };

    public static string GetModuleIcon(string moduleId) => moduleId switch
    {
        "files" => "📁",
        "notes" => "📝",
        "chat" => "💬",
        "contacts" => "👤",
        "calendar" => "📅",
        "photos" => "📷",
        "music" => "🎵",
        "video" => "🎬",
        "tracks" => "📋",
        "ai" => "🤖",
        _ => "🔍"
    };

    public static string FormatModuleName(string moduleId) => moduleId switch
    {
        "files" => "Files",
        "notes" => "Notes",
        "chat" => "Chat",
        "contacts" => "Contacts",
        "calendar" => "Calendar",
        "photos" => "Photos",
        "music" => "Music",
        "video" => "Video",
        "tracks" => "Tracks",
        "ai" => "AI",
        _ => moduleId
    };
}

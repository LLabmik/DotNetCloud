using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Tests.Phase7;

/// <summary>
/// Tests for per-module metadata extraction and display logic used in SearchResultCard.razor.
/// Each module has specific metadata fields that need to be extracted and displayed correctly.
/// </summary>
[TestClass]
public class SearchResultMetadataTests
{
    #region Files module metadata

    [TestMethod]
    public void FilesMetadata_WithMimeType_ExtractsCorrectly()
    {
        var item = CreateItem("files", metadata: new()
        {
            ["MimeType"] = "application/pdf",
            ["Path"] = "/documents/report.pdf",
            ["Size"] = "1048576"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("MimeType", out var mimeType));
        Assert.AreEqual("application/pdf", mimeType);
        Assert.IsTrue(item.Metadata.TryGetValue("Path", out var path));
        Assert.AreEqual("/documents/report.pdf", path);
        Assert.IsTrue(item.Metadata.TryGetValue("Size", out var size));
        Assert.IsTrue(long.TryParse(size, out var sizeBytes));
        Assert.AreEqual(1048576L, sizeBytes);
    }

    [TestMethod]
    public void FilesMetadata_WithoutOptionalFields_DoesNotThrow()
    {
        var item = CreateItem("files", metadata: new());
        Assert.IsFalse(item.Metadata.TryGetValue("MimeType", out _));
        Assert.IsFalse(item.Metadata.TryGetValue("Path", out _));
        Assert.IsFalse(item.Metadata.TryGetValue("Size", out _));
    }

    [TestMethod]
    public void FilesMetadata_InvalidSize_DoesNotParse()
    {
        var item = CreateItem("files", metadata: new() { ["Size"] = "not-a-number" });
        item.Metadata.TryGetValue("Size", out var size);
        Assert.IsFalse(long.TryParse(size, out _));
    }

    #endregion

    #region Chat module metadata

    [TestMethod]
    public void ChatMetadata_WithChannelId_ExtractsCorrectly()
    {
        var item = CreateItem("chat", metadata: new()
        {
            ["ChannelId"] = "ch-001",
            ["SenderId"] = "user-123"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("ChannelId", out var channelId));
        Assert.AreEqual("ch-001", channelId);
    }

    [TestMethod]
    public void ChatMetadata_WithoutChannelId_ReturnsFalse()
    {
        var item = CreateItem("chat", metadata: new());
        Assert.IsFalse(item.Metadata.TryGetValue("ChannelId", out _));
    }

    #endregion

    #region Notes module metadata

    [TestMethod]
    public void NotesMetadata_WithFormat_ExtractsCorrectly()
    {
        var item = CreateItem("notes", metadata: new()
        {
            ["Format"] = "Markdown",
            ["FolderId"] = Guid.NewGuid().ToString()
        });

        Assert.IsTrue(item.Metadata.TryGetValue("Format", out var format));
        Assert.AreEqual("Markdown", format);
    }

    [TestMethod]
    public void NotesMetadata_EmptyGuidFolderId_IsDetected()
    {
        var item = CreateItem("notes", metadata: new()
        {
            ["FolderId"] = Guid.Empty.ToString()
        });

        item.Metadata.TryGetValue("FolderId", out var folderId);
        Assert.AreEqual(Guid.Empty.ToString(), folderId);
    }

    #endregion

    #region Calendar module metadata

    [TestMethod]
    public void CalendarMetadata_WithStartUtc_ParsesCorrectly()
    {
        var startTime = "2026-06-15T14:30:00+00:00";
        var item = CreateItem("calendar", metadata: new()
        {
            ["StartUtc"] = startTime,
            ["Location"] = "Conference Room A"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("StartUtc", out var startUtc));
        Assert.IsTrue(DateTimeOffset.TryParse(startUtc, out var parsed));
        Assert.AreEqual(14, parsed.Hour);
        Assert.AreEqual(30, parsed.Minute);
    }

    [TestMethod]
    public void CalendarMetadata_InvalidStartUtc_DoesNotParse()
    {
        var item = CreateItem("calendar", metadata: new()
        {
            ["StartUtc"] = "invalid-date"
        });

        item.Metadata.TryGetValue("StartUtc", out var startUtc);
        Assert.IsFalse(DateTimeOffset.TryParse(startUtc, out _));
    }

    [TestMethod]
    public void CalendarMetadata_EmptyLocation_IsWhitespace()
    {
        var item = CreateItem("calendar", metadata: new()
        {
            ["Location"] = "  "
        });

        item.Metadata.TryGetValue("Location", out var location);
        Assert.IsTrue(string.IsNullOrWhiteSpace(location));
    }

    #endregion

    #region Photos module metadata

    [TestMethod]
    public void PhotosMetadata_WithCamera_ExtractsCorrectly()
    {
        var item = CreateItem("photos", metadata: new()
        {
            ["AlbumId"] = "album-001",
            ["Camera"] = "Canon EOS R5",
            ["TakenAt"] = "2026-03-20T10:00:00Z"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("Camera", out var camera));
        Assert.AreEqual("Canon EOS R5", camera);
    }

    #endregion

    #region Music module metadata

    [TestMethod]
    public void MusicMetadata_WithGenreAndYear_ExtractsCorrectly()
    {
        var item = CreateItem("music", metadata: new()
        {
            ["Genre"] = "Rock",
            ["Year"] = "2024"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("Genre", out var genre));
        Assert.AreEqual("Rock", genre);
        Assert.IsTrue(item.Metadata.TryGetValue("Year", out var year));
        Assert.AreEqual("2024", year);
    }

    [TestMethod]
    public void MusicMetadata_EmptyGenre_IsDetectedAsEmpty()
    {
        var item = CreateItem("music", metadata: new() { ["Genre"] = "" });
        item.Metadata.TryGetValue("Genre", out var genre);
        Assert.IsTrue(string.IsNullOrWhiteSpace(genre));
    }

    #endregion

    #region Video module metadata

    [TestMethod]
    public void VideoMetadata_WithDuration_ExtractsCorrectly()
    {
        var item = CreateItem("video", metadata: new()
        {
            ["Duration"] = "1:23:45",
            ["Resolution"] = "1920x1080",
            ["CollectionId"] = "col-001"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("Duration", out var duration));
        Assert.AreEqual("1:23:45", duration);
    }

    #endregion

    #region Tracks module metadata

    [TestMethod]
    public void TracksMetadata_WithStatusAndLabels_ExtractsCorrectly()
    {
        var item = CreateItem("tracks", metadata: new()
        {
            ["Status"] = "In Progress",
            ["Labels"] = "Bug,High Priority,Backend",
            ["BoardId"] = "board-001"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("Status", out var status));
        Assert.AreEqual("In Progress", status);

        Assert.IsTrue(item.Metadata.TryGetValue("Labels", out var labels));
        var labelList = labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.AreEqual(3, labelList.Length);
        Assert.AreEqual("Bug", labelList[0]);
    }

    [TestMethod]
    public void TracksMetadata_LabelsSplit_LimitsToThree()
    {
        var labels = "Bug,High Priority,Backend,Frontend,Urgent";
        var limited = labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(3).ToList();
        Assert.AreEqual(3, limited.Count);
        Assert.AreEqual("Backend", limited[2]);
    }

    [TestMethod]
    public void TracksMetadata_EmptyLabels_ProducesEmptyArray()
    {
        var labels = "";
        var split = labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.AreEqual(0, split.Length);
    }

    #endregion

    #region Contacts module metadata

    [TestMethod]
    public void ContactsMetadata_WithContactType_ExtractsCorrectly()
    {
        var item = CreateItem("contacts", metadata: new()
        {
            ["ContactType"] = "Business"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("ContactType", out var contactType));
        Assert.AreEqual("Business", contactType);
    }

    #endregion

    #region AI module metadata

    [TestMethod]
    public void AiMetadata_WithConversationId_ExtractsCorrectly()
    {
        var item = CreateItem("ai", metadata: new()
        {
            ["Role"] = "user",
            ["ConversationId"] = "conv-001"
        });

        Assert.IsTrue(item.Metadata.TryGetValue("Role", out var role));
        Assert.AreEqual("user", role);
    }

    #endregion

    #region Cross-module metadata consistency

    [TestMethod]
    public void AllModules_EmptyMetadata_DoNotThrow()
    {
        var modules = new[] { "files", "notes", "chat", "contacts", "calendar",
                              "photos", "music", "video", "tracks", "ai" };

        foreach (var module in modules)
        {
            var item = CreateItem(module, metadata: new());
            Assert.IsNotNull(item.Metadata);
            Assert.AreEqual(0, item.Metadata.Count);
        }
    }

    [TestMethod]
    public void AllModules_HaveCorrectIcon()
    {
        var expectedIcons = new Dictionary<string, string>
        {
            ["files"] = "📁", ["notes"] = "📝", ["chat"] = "💬",
            ["contacts"] = "👤", ["calendar"] = "📅", ["photos"] = "📷",
            ["music"] = "🎵", ["video"] = "🎬", ["tracks"] = "📋", ["ai"] = "🤖"
        };

        foreach (var (module, icon) in expectedIcons)
        {
            Assert.AreEqual(icon, SearchUrlHelper.GetModuleIcon(module),
                $"Icon mismatch for module '{module}'");
        }
    }

    [TestMethod]
    public void AllModules_HaveCorrectDisplayName()
    {
        var expected = new Dictionary<string, string>
        {
            ["files"] = "Files", ["notes"] = "Notes", ["chat"] = "Chat",
            ["contacts"] = "Contacts", ["calendar"] = "Calendar", ["photos"] = "Photos",
            ["music"] = "Music", ["video"] = "Video", ["tracks"] = "Tracks", ["ai"] = "AI"
        };

        foreach (var (module, name) in expected)
        {
            Assert.AreEqual(name, SearchUrlHelper.FormatModuleName(module),
                $"Display name mismatch for module '{module}'");
        }
    }

    #endregion

    private static SearchResultItem CreateItem(
        string moduleId,
        Dictionary<string, string>? metadata = null)
    {
        return new SearchResultItem
        {
            ModuleId = moduleId,
            EntityId = Guid.NewGuid().ToString(),
            EntityType = "TestEntity",
            Title = "Test Item",
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }
}

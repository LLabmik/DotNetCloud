using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.UI.Shared.Services;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Unit tests for <see cref="SearchResultNavigationHelper"/>.
/// </summary>
[TestClass]
public class SearchResultNavigationHelperTests
{
    [TestMethod]
    public void IsMusicFileSearchResult_WhenFilesResultHasAudioMimeType_ReturnsTrue()
    {
        var item = CreateItem(
            moduleId: "files",
            metadata: new Dictionary<string, string> { ["MimeType"] = "audio/flac" });

        var result = SearchResultNavigationHelper.IsMusicFileSearchResult(item);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMusicFileSearchResult_WhenFilesResultHasMusicExtensionInPath_ReturnsTrue()
    {
        var item = CreateItem(
            moduleId: "files",
            metadata: new Dictionary<string, string> { ["Path"] = "/Music/Albums/song.mp3" });

        var result = SearchResultNavigationHelper.IsMusicFileSearchResult(item);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsMusicFileSearchResult_WhenFilesResultHasNonMusicMetadata_ReturnsFalse()
    {
        var item = CreateItem(
            moduleId: "files",
            title: "document.pdf",
            metadata: new Dictionary<string, string>
            {
                ["MimeType"] = "application/pdf",
                ["Path"] = "/docs/document.pdf"
            });

        var result = SearchResultNavigationHelper.IsMusicFileSearchResult(item);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMusicFileSearchResult_WhenNotFilesModule_ReturnsFalse()
    {
        var item = CreateItem(
            moduleId: "music",
            metadata: new Dictionary<string, string> { ["MimeType"] = "audio/mpeg" });

        var result = SearchResultNavigationHelper.IsMusicFileSearchResult(item);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsMusicFileSearchResult_WhenPathIsDirectoryButTitleHasMusicExtension_ReturnsTrue()
    {
        // Reproduces the real scenario: Path metadata is MaterializedPath (directory),
        // so the Path extension check yields "", but the Title is the filename with .mp3.
        var item = CreateItem(
            moduleId: "files",
            title: "song.mp3",
            metadata: new Dictionary<string, string>
            {
                ["Path"] = "/Documents/Music/",
                ["NodeType"] = "File"
            });

        var result = SearchResultNavigationHelper.IsMusicFileSearchResult(item);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void GetResultUrl_WhenMusicFileAndUserChoosesMusic_ReturnsMusicModuleUrl()
    {
        var item = CreateItem(
            moduleId: "files",
            metadata: new Dictionary<string, string> { ["MimeType"] = "audio/mpeg" });

        var url = SearchResultNavigationHelper.GetResultUrl(item, navToken: 123, openMusicModule: true);

        Assert.AreEqual("/apps/music?fileId=entity-1&_nav=123", url);
    }

    [TestMethod]
    public void GetResultUrl_WhenMusicFileAndUserDoesNotChooseMusic_FallsBackToFilesUrl()
    {
        var item = CreateItem(
            moduleId: "files",
            entityId: "file-42",
            metadata: new Dictionary<string, string> { ["MimeType"] = "audio/mpeg" });

        var url = SearchResultNavigationHelper.GetResultUrl(item, navToken: 999, openMusicModule: false);

        Assert.AreEqual("/apps/files?fileId=file-42&_nav=999", url);
    }

    [TestMethod]
    public void GetResultUrl_WhenNonMusicFileAndOpenMusicFlagSet_StillUsesDefaultRoute()
    {
        var item = CreateItem(
            moduleId: "files",
            entityId: "file-77",
            metadata: new Dictionary<string, string> { ["MimeType"] = "application/pdf" });

        var url = SearchResultNavigationHelper.GetResultUrl(item, navToken: 555, openMusicModule: true);

        Assert.AreEqual("/apps/files?fileId=file-77&_nav=555", url);
    }

    private static SearchResultItem CreateItem(
        string moduleId,
        string entityId = "entity-1",
        string title = "Sample",
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new SearchResultItem
        {
            ModuleId = moduleId,
            EntityId = entityId,
            EntityType = "FileNode",
            Title = title,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }
}

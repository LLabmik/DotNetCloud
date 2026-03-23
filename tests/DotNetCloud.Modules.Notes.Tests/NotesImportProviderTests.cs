using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Notes.Tests;

[TestClass]
public class NotesImportProviderTests
{
    private NotesDbContext _db = null!;
    private NoteService _noteService = null!;
    private NotesImportProvider _provider = null!;
    private CallerContext _caller = null!;
    private Mock<IEventBus> _eventBusMock = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotesDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _noteService = new NoteService(_db, _eventBusMock.Object, NullLogger<NoteService>.Instance);
        _provider = new NotesImportProvider(_noteService, NullLogger<NotesImportProvider>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public void DataType_IsNotes()
    {
        Assert.AreEqual(ImportDataType.Notes, _provider.DataType);
    }

    [TestMethod]
    public async Task PreviewAsync_JsonManifest_ReturnsDryRunReport()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = JsonManifestTwoNotes,
            DryRun = true
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.IsTrue(report.IsDryRun);
        Assert.AreEqual(2, report.TotalItems);
        Assert.AreEqual(2, report.SuccessCount);
        Assert.AreEqual(0, report.FailedCount);
    }

    [TestMethod]
    public async Task PreviewAsync_DoesNotPersistRecords()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = JsonManifestTwoNotes,
            DryRun = true
        };

        await _provider.PreviewAsync(request, _caller);

        var dbCount = await _db.Notes.CountAsync();
        Assert.AreEqual(0, dbCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonManifest_CreatesNotes()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = JsonManifestTwoNotes
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsFalse(report.IsDryRun);
        Assert.AreEqual(2, report.SuccessCount);

        var dbCount = await _db.Notes.CountAsync();
        Assert.AreEqual(2, dbCount);

        foreach (var item in report.Items)
        {
            Assert.IsNotNull(item.RecordId);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_DryRunFlag_DoesNotPersist()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = JsonManifestTwoNotes,
            DryRun = true
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsTrue(report.IsDryRun);
        var dbCount = await _db.Notes.CountAsync();
        Assert.AreEqual(0, dbCount);
    }

    [TestMethod]
    public async Task PreviewAsync_EmptyData_ReturnsEmptyReport()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = ""
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.AreEqual(0, report.TotalItems);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawMarkdown_CreatesSingleNote()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = "# My Important Note\n\nThis is the body content.\n\n- Item 1\n- Item 2"
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.AreEqual(1, report.SuccessCount);
        Assert.AreEqual("My Important Note", report.Items[0].DisplayName);

        var note = await _db.Notes.FirstAsync();
        Assert.AreEqual("My Important Note", note.Title);
        Assert.IsTrue(note.Content.Contains("This is the body content."));
    }

    [TestMethod]
    public async Task ExecuteAsync_RawMarkdownNoHeading_UsesDefaultTitle()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = "Just some plain text without a heading."
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.AreEqual(1, report.SuccessCount);
        Assert.AreEqual("Imported Note", report.Items[0].DisplayName);
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonWithTags_PreservesTags()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = """[{"title": "Tagged Note", "content": "Content here", "tags": ["project", "important"]}]"""
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.AreEqual(1, report.SuccessCount);
        var note = await _db.Notes.Include(n => n.Tags).FirstAsync();
        Assert.AreEqual("Tagged Note", note.Title);
        Assert.AreEqual(2, note.Tags.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonPlainTextFormat_SetsCorrectFormat()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = """[{"title": "Plain Note", "content": "Just text", "format": "plaintext"}]"""
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.AreEqual(1, report.SuccessCount);
        var note = await _db.Notes.FirstAsync();
        Assert.AreEqual(NoteContentFormat.PlainText, note.Format);
    }

    [TestMethod]
    public async Task PreviewAsync_JsonWithMissingTitle_ReportsFailedItem()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = """[{"content": "No title here"}]"""
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.AreEqual(1, report.TotalItems);
        Assert.AreEqual(1, report.FailedCount);
        Assert.IsTrue(report.Items[0].Message!.Contains("title", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithTargetFolder_SetsFolderId()
    {
        // Create a folder first
        var folder = new Models.NoteFolder
        {
            Id = Guid.NewGuid(),
            OwnerId = _caller.UserId,
            Name = "Imported",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.NoteFolders.Add(folder);
        await _db.SaveChangesAsync();

        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = """[{"title": "Folder Note", "content": "In a folder"}]""",
            TargetContainerId = folder.Id
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.AreEqual(1, report.SuccessCount);
        var note = await _db.Notes.FirstAsync();
        Assert.AreEqual(folder.Id, note.FolderId);
    }

    [TestMethod]
    public void ParseNotes_JsonManifest_ParsesCorrectly()
    {
        var parsed = NotesImportProvider.ParseNotes(JsonManifestTwoNotes);

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual("Meeting Notes", parsed[0].Title);
        Assert.AreEqual("Todo List", parsed[1].Title);
    }

    [TestMethod]
    public void ParseNotes_RawMarkdown_ExtractsTitle()
    {
        var parsed = NotesImportProvider.ParseNotes("# Project Ideas\n\nSome ideas here.");

        Assert.AreEqual(1, parsed.Count);
        Assert.AreEqual("Project Ideas", parsed[0].Title);
        Assert.IsTrue(parsed[0].Content.Contains("Some ideas here."));
    }

    [TestMethod]
    public void ParseSingleNote_NoHeading_DefaultsTitle()
    {
        var parsed = NotesImportProvider.ParseSingleNote("Just plain text.");

        Assert.AreEqual("Imported Note", parsed.Title);
        Assert.AreEqual("Just plain text.", parsed.Content);
    }

    [TestMethod]
    public void ParseSingleNote_WithHeading_ExtractsAndRemovesTitle()
    {
        var parsed = NotesImportProvider.ParseSingleNote("# My Title\n\nBody content here.");

        Assert.AreEqual("My Title", parsed.Title);
        Assert.IsFalse(parsed.Content.Contains("# My Title"));
        Assert.IsTrue(parsed.Content.Contains("Body content here."));
    }

    [TestMethod]
    public async Task ExecuteAsync_ReportContainsTimestamps()
    {
        var before = DateTime.UtcNow;
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = """[{"title": "Test", "content": "Content"}]"""
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsTrue(report.StartedAtUtc >= before);
        Assert.IsTrue(report.CompletedAtUtc >= report.StartedAtUtc);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReportSourcePreserved()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Notes,
            Data = """[{"title": "Test", "content": "Content"}]""",
            Source = ImportSource.Nextcloud
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.AreEqual(ImportSource.Nextcloud, report.Source);
    }

    private const string JsonManifestTwoNotes = """
        [
            {"title": "Meeting Notes", "content": "## Sprint Planning\n\n- Review backlog\n- Assign tasks"},
            {"title": "Todo List", "content": "- Buy groceries\n- Fix bug #42", "tags": ["personal"]}
        ]
        """;
}

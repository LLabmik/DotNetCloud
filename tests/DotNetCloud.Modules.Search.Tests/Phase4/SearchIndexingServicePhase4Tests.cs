using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase4;

/// <summary>
/// Phase 4 tests for <see cref="SearchIndexingService"/> — event-driven indexing pipeline
/// with content extraction integration, processing counters, and enrichment.
/// </summary>
[TestClass]
public class SearchIndexingServicePhase4Tests
{
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private Mock<ISearchableModule> _filesModuleMock = null!;
    private Mock<ISearchableModule> _notesModuleMock = null!;
    private Mock<IContentExtractor> _textExtractorMock = null!;
    private ContentExtractionService _extractionService = null!;
    private SearchIndexingService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();

        _filesModuleMock = new Mock<ISearchableModule>();
        _filesModuleMock.Setup(m => m.ModuleId).Returns("files");

        _notesModuleMock = new Mock<ISearchableModule>();
        _notesModuleMock.Setup(m => m.ModuleId).Returns("notes");

        _textExtractorMock = new Mock<IContentExtractor>();
        _textExtractorMock.Setup(e => e.CanExtract("text/plain")).Returns(true);

        _extractionService = new ContentExtractionService(
            [_textExtractorMock.Object],
            NullLogger<ContentExtractionService>.Instance);

        _service = new SearchIndexingService(
            _searchProviderMock.Object,
            [_filesModuleMock.Object, _notesModuleMock.Object],
            _extractionService,
            NullLogger<SearchIndexingService>.Instance);
    }

    // --- Processing Counter Tests ---

    [TestMethod]
    public void TotalProcessed_InitiallyZero()
    {
        Assert.AreEqual(0L, _service.TotalProcessed);
    }

    [TestMethod]
    public void TotalFailed_InitiallyZero()
    {
        Assert.AreEqual(0L, _service.TotalFailed);
    }

    [TestMethod]
    public async Task TotalProcessed_IncrementsOnSuccessfulIndex()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test File");
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        _service.Start();

        await _service.EnqueueAsync(CreateIndexEvent("files", "e1"));
        await Task.Delay(300);
        await _service.StopAsync();

        Assert.AreEqual(1L, _service.TotalProcessed);
        Assert.AreEqual(0L, _service.TotalFailed);
    }

    [TestMethod]
    public async Task TotalProcessed_IncrementsOnRemoveAction()
    {
        _service.Start();

        await _service.EnqueueAsync(CreateRemoveEvent("files", "e1"));
        await Task.Delay(300);
        await _service.StopAsync();

        Assert.AreEqual(1L, _service.TotalProcessed);
    }

    [TestMethod]
    public async Task TotalFailed_IncrementsOnError()
    {
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("e1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        _service.Start();

        await _service.EnqueueAsync(CreateIndexEvent("files", "e1"));
        await Task.Delay(300);
        await _service.StopAsync();

        Assert.AreEqual(0L, _service.TotalProcessed);
        Assert.AreEqual(1L, _service.TotalFailed);
    }

    [TestMethod]
    public async Task Counters_AccumulateMultipleOperations()
    {
        var doc1 = CreateSearchDocument("files", "e1", "FileNode", "File 1");
        var doc2 = CreateSearchDocument("notes", "e2", "Note", "Note 1");

        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc1);
        _notesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("e2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc2);
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("e3", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("error"));

        _service.Start();

        await _service.EnqueueAsync(CreateIndexEvent("files", "e1"));
        await _service.EnqueueAsync(CreateIndexEvent("notes", "e2"));
        await _service.EnqueueAsync(CreateRemoveEvent("files", "r1"));
        await _service.EnqueueAsync(CreateIndexEvent("files", "e3")); // will fail

        await Task.Delay(500);
        await _service.StopAsync();

        Assert.AreEqual(3L, _service.TotalProcessed);
        Assert.AreEqual(1L, _service.TotalFailed);
    }

    // --- Multi-Module Dispatch Tests ---

    [TestMethod]
    public async Task ProcessQueue_RoutesToCorrectModule()
    {
        var fileDoc = CreateSearchDocument("files", "f1", "FileNode", "File");
        var noteDoc = CreateSearchDocument("notes", "n1", "Note", "Note");

        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("f1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDoc);
        _notesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("n1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(noteDoc);

        _service.Start();

        await _service.EnqueueAsync(CreateIndexEvent("files", "f1"));
        await _service.EnqueueAsync(CreateIndexEvent("notes", "n1"));

        await Task.Delay(300);
        await _service.StopAsync();

        _filesModuleMock.Verify(
            m => m.GetSearchableDocumentAsync("f1", It.IsAny<CancellationToken>()), Times.Once);
        _notesModuleMock.Verify(
            m => m.GetSearchableDocumentAsync("n1", It.IsAny<CancellationToken>()), Times.Once);
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task ProcessQueue_SequentialProcessing_MaintainsOrder()
    {
        var callOrder = new List<string>();

        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) =>
            {
                callOrder.Add(id);
                return CreateSearchDocument("files", id, "FileNode", $"File {id}");
            });

        _service.Start();

        for (int i = 1; i <= 5; i++)
        {
            await _service.EnqueueAsync(CreateIndexEvent("files", $"e{i}"));
        }

        await Task.Delay(500);
        await _service.StopAsync();

        CollectionAssert.AreEqual(
            new[] { "e1", "e2", "e3", "e4", "e5" },
            callOrder);
    }

    // --- Content Extraction Integration Tests ---

    [TestMethod]
    public async Task TryEnrichWithContentExtraction_NoMimeType_ReturnsOriginal()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test",
            metadata: new Dictionary<string, string>());

        var result = await _service.TryEnrichWithContentExtraction(doc, CancellationToken.None);

        Assert.AreSame(doc, result);
    }

    [TestMethod]
    public async Task TryEnrichWithContentExtraction_UnsupportedMimeType_ReturnsOriginal()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test",
            metadata: new Dictionary<string, string> { ["MimeType"] = "application/octet-stream" });

        var result = await _service.TryEnrichWithContentExtraction(doc, CancellationToken.None);

        Assert.AreSame(doc, result);
    }

    [TestMethod]
    public async Task TryEnrichWithContentExtraction_HasContent_ReturnsOriginal()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test",
            content: "existing content",
            metadata: new Dictionary<string, string> { ["MimeType"] = "text/plain" });

        var result = await _service.TryEnrichWithContentExtraction(doc, CancellationToken.None);

        Assert.AreSame(doc, result);
    }

    [TestMethod]
    public async Task TryEnrichWithContentExtraction_LowercaseMimeTypeKey_RecognizesMetadata()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test",
            metadata: new Dictionary<string, string> { ["mimeType"] = "application/octet-stream" });

        var result = await _service.TryEnrichWithContentExtraction(doc, CancellationToken.None);

        // Should attempt extraction check (returns original since type is unsupported)
        Assert.AreSame(doc, result);
    }

    [TestMethod]
    public async Task EnrichDocumentFromStreamAsync_ExtractsText()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test");

        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent
            {
                Text = "extracted text from file",
                Metadata = new Dictionary<string, string> { ["author"] = "Test Author" }
            });

        using var stream = new MemoryStream("Hello world"u8.ToArray());
        var result = await _service.EnrichDocumentFromStreamAsync(doc, stream, "text/plain");

        Assert.AreEqual("extracted text from file", result.Content);
        Assert.IsTrue(result.Metadata.ContainsKey("author"));
        Assert.AreEqual("Test Author", result.Metadata["author"]);
    }

    [TestMethod]
    public async Task EnrichDocumentFromStreamAsync_MergesMetadata()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test",
            metadata: new Dictionary<string, string> { ["path"] = "/docs/test.txt" });

        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent
            {
                Text = "content",
                Metadata = new Dictionary<string, string> { ["encoding"] = "utf-8" }
            });

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.EnrichDocumentFromStreamAsync(doc, stream, "text/plain");

        Assert.AreEqual("/docs/test.txt", result.Metadata["path"]);
        Assert.AreEqual("utf-8", result.Metadata["encoding"]);
    }

    [TestMethod]
    public async Task EnrichDocumentFromStreamAsync_ExistingMetadataNotOverwritten()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test",
            metadata: new Dictionary<string, string> { ["author"] = "Original Author" });

        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent
            {
                Text = "content",
                Metadata = new Dictionary<string, string> { ["author"] = "Extracted Author" }
            });

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.EnrichDocumentFromStreamAsync(doc, stream, "text/plain");

        // Original metadata should be preserved (TryAdd semantics)
        Assert.AreEqual("Original Author", result.Metadata["author"]);
    }

    [TestMethod]
    public async Task EnrichDocumentFromStreamAsync_ExtractionFails_ReturnsOriginal()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test");

        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtractedContent?)null);

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.EnrichDocumentFromStreamAsync(doc, stream, "text/plain");

        Assert.AreSame(doc, result);
    }

    // --- Backpressure & Channel Tests ---

    [TestMethod]
    public async Task EnqueueAsync_MultipleBurst_AllProcessed()
    {
        var docs = Enumerable.Range(1, 50).Select(i =>
            CreateSearchDocument("files", $"e{i}", "FileNode", $"File {i}")).ToList();

        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) =>
                docs.FirstOrDefault(d => d.EntityId == id));

        _service.Start();

        foreach (var doc in docs)
        {
            await _service.EnqueueAsync(CreateIndexEvent("files", doc.EntityId));
        }

        // Give time for processing
        await Task.Delay(2000);
        await _service.StopAsync();

        Assert.AreEqual(50L, _service.TotalProcessed);
    }

    [TestMethod]
    public async Task ProcessQueue_ErrorDoesNotStopProcessing()
    {
        var doc1 = CreateSearchDocument("files", "good1", "FileNode", "Good 1");
        var doc2 = CreateSearchDocument("files", "good2", "FileNode", "Good 2");

        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("good1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc1);
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("error"));
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("good2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc2);

        _service.Start();

        await _service.EnqueueAsync(CreateIndexEvent("files", "good1"));
        await _service.EnqueueAsync(CreateIndexEvent("files", "bad"));
        await _service.EnqueueAsync(CreateIndexEvent("files", "good2"));

        await Task.Delay(500);
        await _service.StopAsync();

        // Both good items should be processed, bad one should fail
        Assert.AreEqual(2L, _service.TotalProcessed);
        Assert.AreEqual(1L, _service.TotalFailed);

        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.Is<SearchDocument>(d => d.EntityId == "good1"), It.IsAny<CancellationToken>()),
            Times.Once);
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.Is<SearchDocument>(d => d.EntityId == "good2"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- Document Not Found Cascade ---

    [TestMethod]
    public async Task ProcessQueue_IndexAction_EntityDeletedBetweenEventAndProcessing_RemovesFromIndex()
    {
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("deleted-entity", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchDocument?)null);

        _service.Start();

        await _service.EnqueueAsync(CreateIndexEvent("files", "deleted-entity"));
        await Task.Delay(300);
        await _service.StopAsync();

        _searchProviderMock.Verify(
            p => p.RemoveDocumentAsync("files", "deleted-entity", It.IsAny<CancellationToken>()),
            Times.Once);
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // --- Lifecycle Tests ---

    [TestMethod]
    public async Task StopAsync_GracefulShutdown_ProcessesPendingItems()
    {
        var doc = CreateSearchDocument("files", "e1", "FileNode", "Test");
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        await _service.EnqueueAsync(CreateIndexEvent("files", "e1"));
        Assert.AreEqual(1, _service.PendingCount);

        _service.Start();
        await Task.Delay(300);
        await _service.StopAsync();

        // Item should have been processed
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(doc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- Helpers ---

    private static SearchIndexRequestEvent CreateIndexEvent(string moduleId, string entityId) => new()
    {
        EventId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        ModuleId = moduleId,
        EntityId = entityId,
        Action = SearchIndexAction.Index
    };

    private static SearchIndexRequestEvent CreateRemoveEvent(string moduleId, string entityId) => new()
    {
        EventId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        ModuleId = moduleId,
        EntityId = entityId,
        Action = SearchIndexAction.Remove
    };

    private static SearchDocument CreateSearchDocument(
        string moduleId, string entityId, string entityType, string title,
        string content = "", IReadOnlyDictionary<string, string>? metadata = null) => new()
    {
        ModuleId = moduleId,
        EntityId = entityId,
        EntityType = entityType,
        Title = title,
        Content = content,
        OwnerId = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        Metadata = metadata ?? new Dictionary<string, string>()
    };
}

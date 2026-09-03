using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Host.Protos;
using DotNetCloud.Modules.Notes.Host.Services;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Modules.Notes.Services;
using DotNetCloud.UI.Shared.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotesGrpcService = DotNetCloud.Modules.Notes.Host.Services.NotesGrpcService;

namespace DotNetCloud.Modules.Notes.Tests;

/// <summary>
/// Tests for <see cref="DotNetCloud.Modules.Notes.Host.Services.NotesGrpcService"/> search-document pull (used by the core
/// search indexer's full reindex and real-time incremental indexing). Regression
/// coverage for the fix that routes these RPCs through <see cref="NotesDbContext"/>
/// instead of owner-scoped service methods (which returned nothing for a system caller).
/// </summary>
[TestClass]
public class NotesGrpcServiceSearchDocumentTests
{
    private NotesDbContext _db = null!;
    private NotesGrpcService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new NotesDbContext(options);

        _service = new NotesGrpcService(
            Mock.Of<INoteService>(),
            Mock.Of<INoteFolderService>(),
            Mock.Of<INoteShareService>(),
            Mock.Of<IMarkdownRenderer>(),
            _db,
            NullLogger<NotesGrpcService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private static Note CreateNote(string title, string content, string? tag = null)
    {
        var note = new Note
        {
            OwnerId = Guid.CreateVersion7(),
            Title = title,
            Content = content
        };

        if (tag is not null)
            note.Tags.Add(new NoteTag { Tag = tag });

        return note;
    }

    [TestMethod]
    public async Task GetSearchableDocuments_ReturnsNotesAcrossAllOwners()
    {
        _db.Notes.Add(CreateNote("Note A", "alpha body"));
        _db.Notes.Add(CreateNote("Note B", "beta body", "work"));
        await _db.SaveChangesAsync();

        var stream = new CollectingSearchableDocumentStream();

        await _service.GetSearchableDocuments(
            new GetSearchableDocumentsRequest(), stream, new TestServerCallContext());

        // Regression: the previous owner-scoped implementation returned zero
        // documents for the system caller used by the search indexer.
        Assert.AreEqual(2, stream.Documents.Count);
        CollectionAssert.AreEquivalent(
            new[] { "Note A", "Note B" },
            stream.Documents.Select(d => d.Title).ToArray());
        Assert.IsTrue(stream.Documents.All(d => d.ModuleId == "notes"));
        Assert.IsTrue(stream.Documents.All(d => !string.IsNullOrEmpty(d.OwnerId)));
    }

    [TestMethod]
    public async Task GetSearchableDocument_ExistingNote_ReturnsDocumentWithMetadata()
    {
        var note = CreateNote("Secret Alpha", "secret body", "todo");
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        var response = await _service.GetSearchableDocument(
            new GetSearchableDocumentRequest { EntityId = note.Id.ToString() },
            new TestServerCallContext());

        Assert.IsTrue(response.Found);
        Assert.AreEqual("notes", response.Document.ModuleId);
        Assert.AreEqual(note.Id.ToString(), response.Document.EntityId);
        Assert.AreEqual("Secret Alpha", response.Document.Title);
        Assert.AreEqual("todo", response.Document.Metadata["Tags"]);
    }

    [TestMethod]
    public async Task GetSearchableDocument_UnknownId_ReturnsNotFound()
    {
        var response = await _service.GetSearchableDocument(
            new GetSearchableDocumentRequest { EntityId = Guid.CreateVersion7().ToString() },
            new TestServerCallContext());

        Assert.IsFalse(response.Found);
    }

    [TestMethod]
    public async Task GetSearchableDocument_SoftDeletedNote_ReturnsNotFound()
    {
        var note = CreateNote("Deleted", "body");
        note.IsDeleted = true;
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        var response = await _service.GetSearchableDocument(
            new GetSearchableDocumentRequest { EntityId = note.Id.ToString() },
            new TestServerCallContext());

        Assert.IsFalse(response.Found);
    }

    /// <summary>
    /// Collects documents written to a server-streaming response.
    /// </summary>
    private sealed class CollectingSearchableDocumentStream : IServerStreamWriter<SearchableDocument>
    {
        public List<SearchableDocument> Documents { get; } = [];

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(SearchableDocument message)
        {
            Documents.Add(message);
            return Task.CompletedTask;
        }

        public Task WriteAsync(SearchableDocument message, CancellationToken cancellationToken)
        {
            Documents.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Minimal mock of <see cref="ServerCallContext"/> for unit testing gRPC services.
    /// </summary>
    private sealed class TestServerCallContext : ServerCallContext
    {
        protected override string MethodCore => "test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore { get => Status.DefaultSuccess; set { } }
        protected override WriteOptions? WriteOptionsCore { get => null; set { } }
        protected override AuthContext AuthContextCore => new("test", []);
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}

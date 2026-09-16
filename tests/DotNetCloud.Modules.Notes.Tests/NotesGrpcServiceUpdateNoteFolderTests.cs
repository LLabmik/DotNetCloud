using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Host.Protos;
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
/// Tests that <see cref="DotNetCloud.Modules.Notes.Host.Services.NotesGrpcService"/> maps the
/// folder fields of <see cref="UpdateNoteRequest"/> onto <see cref="UpdateNoteDto"/> — covering
/// both moving a note into a folder and taking it out of one ("unfiled").
/// </summary>
[TestClass]
public class NotesGrpcServiceUpdateNoteFolderTests
{
    private NotesDbContext _db = null!;
    private Mock<INoteService> _noteService = null!;
    private NotesGrpcService _grpcService = null!;

    private static readonly Guid NoteId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new NotesDbContext(options);

        _noteService = new Mock<INoteService>();
        _grpcService = new NotesGrpcService(
            _noteService.Object,
            Mock.Of<INoteFolderService>(),
            Mock.Of<INoteShareService>(),
            Mock.Of<IMarkdownRenderer>(),
            _db,
            NullLogger<NotesGrpcService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    /// <summary>
    /// Captures the DTO handed to <see cref="INoteService.UpdateNoteAsync"/> and returns a
    /// minimal note so the RPC succeeds.
    /// </summary>
    private void CaptureUpdateDto(List<UpdateNoteDto> captured)
    {
        _noteService
            .Setup(s => s.UpdateNoteAsync(
                It.IsAny<Guid>(),
                It.IsAny<UpdateNoteDto>(),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, UpdateNoteDto, CallerContext, CancellationToken>(
                (_, dto, _, _) => captured.Add(dto))
            .ReturnsAsync(new NoteDto
            {
                Id = NoteId,
                OwnerId = UserId,
                Title = "Note",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
    }

    [TestMethod]
    public async Task UpdateNote_WithFolderId_MapsFolderId()
    {
        var folderId = Guid.CreateVersion7();
        var captured = new List<UpdateNoteDto>();
        CaptureUpdateDto(captured);

        var response = await _grpcService.UpdateNote(
            new UpdateNoteRequest
            {
                NoteId = NoteId.ToString(),
                UserId = UserId.ToString(),
                FolderId = folderId.ToString()
            },
            new TestServerCallContext());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(1, captured.Count);
        Assert.AreEqual(folderId, captured[0].FolderId);
        Assert.IsFalse(captured[0].ClearFolder);
    }

    [TestMethod]
    public async Task UpdateNote_WithClearFolder_MapsClearFlag()
    {
        var captured = new List<UpdateNoteDto>();
        CaptureUpdateDto(captured);

        var response = await _grpcService.UpdateNote(
            new UpdateNoteRequest
            {
                NoteId = NoteId.ToString(),
                UserId = UserId.ToString(),
                FolderId = string.Empty,
                ClearFolder = true
            },
            new TestServerCallContext());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(1, captured.Count);
        Assert.IsNull(captured[0].FolderId);
        Assert.IsTrue(captured[0].ClearFolder);
    }

    [TestMethod]
    public async Task UpdateNote_WithoutFolderFields_LeavesFolderUntouched()
    {
        var captured = new List<UpdateNoteDto>();
        CaptureUpdateDto(captured);

        var response = await _grpcService.UpdateNote(
            new UpdateNoteRequest
            {
                NoteId = NoteId.ToString(),
                UserId = UserId.ToString(),
                Title = "Renamed"
            },
            new TestServerCallContext());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(1, captured.Count);
        Assert.IsNull(captured[0].FolderId);
        Assert.IsFalse(captured[0].ClearFolder);
        Assert.AreEqual("Renamed", captured[0].Title);
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

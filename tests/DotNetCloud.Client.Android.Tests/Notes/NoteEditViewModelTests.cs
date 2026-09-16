using System.Net.Http;
using System.Text.Json;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.Android.Tests.Notes;

/// <summary>
/// Covers the note editor's folder picker: loading the caller's folders in create mode as well as
/// edit mode, pre-selecting the note's folder, and the move / unfile semantics sent to the server
/// (a bare <c>null</c> folder means "no change", so unfiling needs <c>ClearFolder</c>).
/// </summary>
[TestClass]
public sealed class NoteEditViewModelTests
{
    private const string ServerUrl = "https://cloud.example.com";
    private const string AccessToken = "test-access-token";

    private static readonly Guid FolderAId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid FolderBId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid OwnersFolderId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
    private static readonly Guid NoteId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");

    private Mock<INotesRestClient> _notesApi = null!;
    private Mock<IOfflineOperationQueue> _queue = null!;
    private Mock<IConnectivityMonitor> _connectivity = null!;
    private NoteEditViewModel _vm = null!;

    [TestInitialize]
    public void Setup()
    {
        _notesApi = new Mock<INotesRestClient>(MockBehavior.Loose);
        _queue = new Mock<IOfflineOperationQueue>(MockBehavior.Loose);
        _connectivity = new Mock<IConnectivityMonitor>(MockBehavior.Loose);
        _connectivity.SetupGet(c => c.IsOnline).Returns(true);

        SetupFolders();

        var serverStore = new Mock<IServerConnectionStore>(MockBehavior.Loose);
        serverStore.Setup(s => s.GetActive())
            .Returns(new ServerConnection(ServerUrl, "Test Server", "user@example.com"));

        var tokenStore = new Mock<ISecureTokenStore>(MockBehavior.Loose);
        tokenStore.Setup(t => t.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessToken);

        _vm = new NoteEditViewModel(
            _notesApi.Object,
            _queue.Object,
            _connectivity.Object,
            serverStore.Object,
            tokenStore.Object,
            NullLogger<NoteEditViewModel>.Instance);
    }

    // ── Load: create mode ──────────────────────────────────────────

    [TestMethod]
    public async Task LoadAsync_CreateMode_LoadsFoldersForThePicker()
    {
        SetupFolders(("A", FolderAId), ("B", FolderBId));

        await _vm.LoadCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(
            new[] { NoteEditViewModel.NoFolderLabel, "A", "B" },
            _vm.FolderOptions.Select(o => o.Name).ToArray());

        Assert.AreEqual(NoteEditViewModel.NoFolderLabel, _vm.SelectedFolderOption?.Name);
        Assert.IsNull(_vm.SelectedFolderId);
    }

    [TestMethod]
    public async Task LoadAsync_CreateMode_DoesNotFetchANote()
    {
        await _vm.LoadCommand.ExecuteAsync(null);

        _notesApi.Verify(
            a => a.GetNoteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task LoadAsync_CreateMode_WithFolderQuery_StartsInThatFolder()
    {
        SetupFolders(("A", FolderAId), ("B", FolderBId));
        _vm.FolderId = FolderBId.ToString();

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual(FolderBId, _vm.SelectedFolderId);
        Assert.AreEqual("B", _vm.SelectedFolderOption?.Name);
        Assert.AreEqual(FolderBId, _vm.BuildCreateDto().FolderId);
    }

    [TestMethod]
    public async Task LoadAsync_FolderLoadFailure_StillAllowsEditing()
    {
        _notesApi
            .Setup(a => a.ListFoldersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("folder listing unavailable"));

        await _vm.LoadCommand.ExecuteAsync(null);

        // The picker degrades to "None (unfiled)" instead of blocking the editor.
        CollectionAssert.AreEqual(
            new[] { NoteEditViewModel.NoFolderLabel },
            _vm.FolderOptions.Select(o => o.Name).ToArray());
        Assert.IsNull(_vm.ErrorMessage);
    }

    // ── Load: edit mode ────────────────────────────────────────────

    [TestMethod]
    public async Task LoadAsync_EditMode_SelectsTheNotesFolder()
    {
        SetupFolders(("A", FolderAId), ("B", FolderBId));
        SetupNote(CreateNote(FolderAId, version: 7));

        _vm.NoteId = NoteId.ToString();
        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("My note", _vm.Title);
        Assert.AreEqual(FolderAId, _vm.SelectedFolderId);
        Assert.AreEqual("A", _vm.SelectedFolderOption?.Name);
        Assert.IsTrue(_vm.CanEditFolder);
        Assert.AreEqual(7, _vm.BuildUpdateDto().ExpectedVersion);
    }

    [TestMethod]
    public async Task LoadAsync_EditMode_UnfiledNote_SelectsNone()
    {
        SetupNote(CreateNote(folderId: null, version: 2));

        _vm.NoteId = NoteId.ToString();
        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.IsNull(_vm.SelectedFolderId);
        Assert.AreEqual(NoteEditViewModel.NoFolderLabel, _vm.SelectedFolderOption?.Name);
    }

    [TestMethod]
    public async Task LoadAsync_EditMode_FolderMissingFromCallersList_IsStillShown()
    {
        // Folders belong to the note's owner, so a note shared with the caller can be filed in a
        // folder the caller cannot list. Showing it keeps the note filed when the note is saved.
        SetupNote(CreateNote(OwnersFolderId, version: 1));

        _vm.NoteId = NoteId.ToString();
        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual(OwnersFolderId, _vm.SelectedFolderId);
        Assert.AreEqual(NoteFolderLabels.SharedFolderFallback, _vm.SelectedFolderOption?.Name);
    }

    [TestMethod]
    public async Task LoadAsync_EditMode_SharedNote_HidesTheFolderPicker()
    {
        SetupNote(CreateNote(OwnersFolderId, version: 1, viewerPermission: NoteSharePermission.ReadWrite));

        _vm.NoteId = NoteId.ToString();
        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.IsFalse(_vm.CanEditFolder);
    }

    [TestMethod]
    public async Task LoadAsync_EditMode_SharedNoteUnfilesNothingWhenSaved()
    {
        SetupFolders(("A", FolderAId));
        SetupNote(CreateNote(OwnersFolderId, version: 1, viewerPermission: NoteSharePermission.ReadWrite));

        _vm.NoteId = NoteId.ToString();
        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.Title = "Renamed by a sharee";

        var dto = _vm.BuildUpdateDto();

        Assert.IsNull(dto.FolderId);
        Assert.IsFalse(dto.ClearFolder);
    }

    // ── Save: folder semantics ─────────────────────────────────────

    [TestMethod]
    public void BuildUpdateDto_SelectingNone_RequestsClearFolder()
    {
        _vm.Title = "Unfile me";
        _vm.SelectedFolderOption = _vm.FolderOptions.First(o => o.Id is null);

        var dto = _vm.BuildUpdateDto();

        Assert.IsNull(dto.FolderId);
        Assert.IsTrue(dto.ClearFolder);
    }

    [TestMethod]
    public void BuildUpdateDto_SelectingAFolder_SendsTheFolderWithoutClearFolder()
    {
        _vm.Title = "File me";
        _vm.FolderOptions.Add(new NoteFolderOption(FolderAId, "A"));
        _vm.SelectedFolderOption = _vm.FolderOptions.Last();

        var dto = _vm.BuildUpdateDto();

        Assert.AreEqual(FolderAId, dto.FolderId);
        Assert.IsFalse(dto.ClearFolder);
    }

    [TestMethod]
    public void SelectedFolderOption_KeepsSelectedFolderIdInSync()
    {
        _vm.FolderOptions.Add(new NoteFolderOption(FolderAId, "A"));

        _vm.SelectedFolderOption = _vm.FolderOptions.Last();

        Assert.AreEqual(FolderAId, _vm.SelectedFolderId);
    }

    [TestMethod]
    public void SelectedFolderId_KeepsThePickerSelectionInSync()
    {
        _vm.FolderOptions.Add(new NoteFolderOption(FolderAId, "A"));

        _vm.SelectedFolderId = FolderAId;

        Assert.AreEqual(FolderAId, _vm.SelectedFolderOption?.Id);
    }

    [TestMethod]
    public async Task SaveAsync_EditMode_Unfiling_SendsClearFolderToTheServer()
    {
        SetupFolders(("A", FolderAId), ("B", FolderBId));
        SetupNote(CreateNote(FolderAId, version: 4));
        SetupUpdateResponse(CreateNote(FolderAId, version: 5));

        _vm.NoteId = NoteId.ToString();
        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.SelectedFolderOption = _vm.FolderOptions.First(o => o.Id is null);

        await _vm.SaveCommand.ExecuteAsync(null);

        _notesApi.Verify(a => a.UpdateNoteAsync(
            ServerUrl,
            AccessToken,
            NoteId,
            It.Is<UpdateNoteDto>(d => d.FolderId == null && d.ClearFolder),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SaveAsync_EditMode_MovingFolder_SendsTheNewFolderId()
    {
        SetupFolders(("A", FolderAId), ("B", FolderBId));
        SetupNote(CreateNote(FolderAId, version: 4));
        SetupUpdateResponse(CreateNote(FolderBId, version: 5));

        _vm.NoteId = NoteId.ToString();
        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.SelectedFolderOption = _vm.FolderOptions.First(o => o.Id == FolderBId);

        await _vm.SaveCommand.ExecuteAsync(null);

        _notesApi.Verify(a => a.UpdateNoteAsync(
            ServerUrl,
            AccessToken,
            NoteId,
            It.Is<UpdateNoteDto>(d => d.FolderId == FolderBId && !d.ClearFolder),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SaveAsync_CreateMode_SendsTheSelectedFolder()
    {
        SetupFolders(("A", FolderAId), ("B", FolderBId));
        SetupCreateResponse(CreateNote(FolderBId, version: 1));

        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.Title = "New note";
        _vm.Content = "Body";
        _vm.SelectedFolderOption = _vm.FolderOptions.First(o => o.Id == FolderBId);

        await _vm.SaveCommand.ExecuteAsync(null);

        _notesApi.Verify(a => a.CreateNoteAsync(
            ServerUrl,
            AccessToken,
            It.Is<CreateNoteDto>(d => d.FolderId == FolderBId && d.Title == "New note"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Save: offline queue parity ─────────────────────────────────

    [TestMethod]
    public async Task SaveAsync_Offline_QueuesTheClearedFolderForReplay()
    {
        SetupFolders(("A", FolderAId));
        SetupNote(CreateNote(FolderAId, version: 4));
        _connectivity.SetupGet(c => c.IsOnline).Returns(false);

        _vm.NoteId = NoteId.ToString();
        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.SelectedFolderOption = _vm.FolderOptions.First(o => o.Id is null);

        var queued = await CaptureQueuedOperationAsync(() => _vm.SaveCommand.ExecuteAsync(null));

        Assert.AreEqual(OfflineOperationType.NoteUpdate, queued.Type);
        var payload = JsonSerializer.Deserialize<OfflineNoteUpdatePayload>(queued.Json)!;
        Assert.AreEqual(NoteId, payload.NoteId);
        Assert.IsTrue(payload.Dto.ClearFolder, "A queued move-to-unfiled must carry ClearFolder.");
        Assert.IsNull(payload.Dto.FolderId);
    }

    [TestMethod]
    public async Task SaveAsync_Offline_QueuesTheCreateWithItsFolder()
    {
        SetupFolders(("A", FolderAId));
        _connectivity.SetupGet(c => c.IsOnline).Returns(false);

        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.Title = "Offline note";
        _vm.SelectedFolderOption = _vm.FolderOptions.First(o => o.Id == FolderAId);

        var queued = await CaptureQueuedOperationAsync(() => _vm.SaveCommand.ExecuteAsync(null));

        Assert.AreEqual(OfflineOperationType.NoteCreate, queued.Type);
        var payload = JsonSerializer.Deserialize<OfflineNoteCreatePayload>(queued.Json)!;
        Assert.AreEqual(FolderAId, payload.Dto.FolderId);
    }

    [TestMethod]
    public async Task SaveAsync_WithoutATitle_ReportsTheValidationErrorAndDoesNotCallTheApi()
    {
        SetupFolders(("A", FolderAId));

        _vm.Title = "   ";
        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.AreEqual("Title is required.", _vm.ErrorMessage);
        _notesApi.Verify(
            a => a.CreateNoteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CreateNoteDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Runs a save and returns the queued offline operation. The post-save navigation needs a live
    /// <c>Shell</c>, which the unit-test host does not have — the queued payload is the contract
    /// under test, so assertions stop there.
    /// </summary>
    private async Task<(OfflineOperationType Type, string Json)> CaptureQueuedOperationAsync(Func<Task> save)
    {
        var captured = new List<(OfflineOperationType Type, string Json)>();
        _queue
            .Setup(q => q.EnqueueAsync(It.IsAny<OfflineOperationType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<OfflineOperationType, string, CancellationToken>((type, json, _) => captured.Add((type, json)))
            .Returns(Task.CompletedTask);

        await save();

        Assert.AreEqual(1, captured.Count, "Expected exactly one queued offline operation.");
        return captured[0];
    }

    private void SetupFolders(params (string Name, Guid Id)[] folders)
    {
        var dtos = folders
            .Select(f => new NoteFolderDto
            {
                Id = f.Id,
                OwnerId = Guid.NewGuid(),
                Name = f.Name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        _notesApi
            .Setup(a => a.ListFoldersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos);
    }

    private static NoteDto CreateNote(
        Guid? folderId,
        int version,
        NoteSharePermission? viewerPermission = null) => new()
    {
        Id = NoteId,
        OwnerId = Guid.NewGuid(),
        FolderId = folderId,
        ViewerPermission = viewerPermission,
        Title = "My note",
        Content = "Body",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Version = version
    };

    private void SetupNote(NoteDto note) =>
        _notesApi
            .Setup(a => a.GetNoteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

    private void SetupUpdateResponse(NoteDto note) =>
        _notesApi
            .Setup(a => a.UpdateNoteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<UpdateNoteDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

    private void SetupCreateResponse(NoteDto note) =>
        _notesApi
            .Setup(a => a.CreateNoteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CreateNoteDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);
}

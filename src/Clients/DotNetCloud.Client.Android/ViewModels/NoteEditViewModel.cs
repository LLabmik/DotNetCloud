using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Messages;
using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>ViewModel for creating and editing a note.</summary>
[QueryProperty(nameof(NoteId), "NoteId")]
[QueryProperty(nameof(FolderId), "FolderId")]
public sealed partial class NoteEditViewModel : ObservableObject
{
    private readonly INotesRestClient _notesApi;
    private readonly IOfflineOperationQueue _offlineQueue;
    private readonly IConnectivityMonitor _connectivity;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<NoteEditViewModel> _logger;

    private int _currentVersion;
    private bool _loaded;
    private Guid? _queryFolderId;
    private bool _syncingFolderSelection;

    /// <summary>Initializes a new <see cref="NoteEditViewModel"/>.</summary>
    public NoteEditViewModel(
        INotesRestClient notesApi,
        IOfflineOperationQueue offlineQueue,
        IConnectivityMonitor connectivity,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<NoteEditViewModel> logger)
    {
        _notesApi = notesApi;
        _offlineQueue = offlineQueue;
        _connectivity = connectivity;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;

        FolderOptions.Add(new NoteFolderOption(null, NoFolderLabel));
    }

    /// <summary>Display label of the picker entry that leaves a note unfiled.</summary>
    public const string NoFolderLabel = "None (unfiled)";

    // ── Query Properties ───────────────────────────────────────────

    /// <summary>Note ID for edit mode. Null/empty = create mode.</summary>
    private string? _noteId;
    public string? NoteId
    {
        get => _noteId;
        set
        {
            _noteId = value;
            IsEditing = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>
    /// Folder the new note should start in (query parameter supplied by the notes list when a
    /// folder chip is active). Ignored in edit mode — the note's own folder wins there.
    /// </summary>
    public string? FolderId
    {
        get => _queryFolderId?.ToString();
        set => _queryFolderId = Guid.TryParse(value, out var id) ? id : null;
    }

    // ── View State ─────────────────────────────────────────────────

    /// <summary>Whether data is loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Error message to display, or null.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Whether we're in edit mode (vs. create mode).</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>Note title.</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>Note markdown content.</summary>
    [ObservableProperty]
    private string _content = string.Empty;

    /// <summary>Rendered HTML for preview.</summary>
    [ObservableProperty]
    private string _previewHtml = string.Empty;

    /// <summary>Whether the preview WebView is visible (vs. the Editor).</summary>
    [ObservableProperty]
    private bool _isPreviewVisible;

    /// <summary>Selected folder ID for this note. Null = no folder.</summary>
    [ObservableProperty]
    private Guid? _selectedFolderId;

    /// <summary>
    /// Whether the caller may change this note's folder. Folders belong to the note's owner, so a
    /// note shared with the caller keeps the owner's folder and the picker is hidden.
    /// </summary>
    [ObservableProperty]
    private bool _canEditFolder = true;

    /// <summary>Entries for the folder picker — "None (unfiled)" first, then the caller's folders.</summary>
    public ObservableCollection<NoteFolderOption> FolderOptions { get; } = [];

    /// <summary>Currently selected folder picker entry.</summary>
    [ObservableProperty]
    private NoteFolderOption? _selectedFolderOption;

    /// <summary>Mirrors the picker selection onto <see cref="SelectedFolderId"/>.</summary>
    partial void OnSelectedFolderOptionChanged(NoteFolderOption? value)
    {
        if (_syncingFolderSelection)
            return;

        SelectedFolderId = value?.Id;
    }

    /// <summary>Mirrors <see cref="SelectedFolderId"/> back onto the picker selection.</summary>
    partial void OnSelectedFolderIdChanged(Guid? value) => SyncSelectedFolderOption();

    // ── Commands ───────────────────────────────────────────────────

    /// <summary>
    /// Loads the note for editing, and the folder list for the picker. Called when the page
    /// appears. In create mode (no NoteId) the note itself is not fetched — only the folders are,
    /// so a brand-new note can still be filed straight away.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        if (_loaded)
            return;
        _loaded = true;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await LoadFolderOptionsAsync(ct);

            if (!IsEditing || string.IsNullOrEmpty(NoteId))
            {
                // Create mode: inherit the folder the caller was browsing, when one was supplied.
                if (_queryFolderId is { } startFolderId)
                {
                    AddMissingFolderOption(startFolderId);
                    SelectedFolderId = startFolderId;
                }

                return;
            }

            if (!Guid.TryParse(NoteId, out var noteId))
                return;

            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var note = await _notesApi.GetNoteAsync(serverUrl, token, noteId, ct);
            Title = note.Title;
            Content = note.Content;

            // Folders are per-user, so only the note's owner can move it between them.
            CanEditFolder = note.ViewerPermission is null;

            if (note.FolderId is { } folderId)
            {
                // A note shared with the caller keeps the owner's folder, which is not in the
                // caller's folder list — show it so saving never silently unfiles the note.
                AddMissingFolderOption(folderId);
            }

            SelectedFolderId = note.FolderId;
            _currentVersion = note.Version;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load note for editing.");
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Saves the note — creates or updates depending on mode.</summary>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Title is required.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // If the device has no signal, persist the note to the offline queue so it is
            // delivered once connectivity returns. The UI navigates away optimistically.
            if (!_connectivity.IsOnline)
            {
                if (IsEditing && Guid.TryParse(NoteId, out var offlineNoteId))
                {
                    await _offlineQueue.EnqueueAsync(OfflineOperationType.NoteUpdate,
                        JsonSerializer.Serialize(new OfflineNoteUpdatePayload(offlineNoteId, BuildUpdateDto())), ct).ConfigureAwait(false);
                }
                else
                {
                    await _offlineQueue.EnqueueAsync(OfflineOperationType.NoteCreate,
                        JsonSerializer.Serialize(new OfflineNoteCreatePayload(BuildCreateDto())), ct).ConfigureAwait(false);
                }

                var isNewOffline = !IsEditing;
                await Shell.Current.GoToAsync("..");
                WeakReferenceMessenger.Default.Send(new NoteSavedMessage(isNewOffline));
                return;
            }

            var (serverUrl, token) = await GetCredentialsAsync(ct);

            if (IsEditing && Guid.TryParse(NoteId, out var noteId))
            {
                await _notesApi.UpdateNoteAsync(serverUrl, token, noteId, BuildUpdateDto(), ct);
            }
            else
            {
                await _notesApi.CreateNoteAsync(serverUrl, token, BuildCreateDto(), ct);
            }

            bool isNew = !IsEditing;
            await Shell.Current.GoToAsync("..");

            // Notify the notes list to refresh
            WeakReferenceMessenger.Default.Send(new NoteSavedMessage(isNew));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save note.");
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Renders the current content as markdown for live preview.</summary>
    [RelayCommand]
    private async Task RenderPreviewAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            PreviewHtml = "<p style='color:#94A3B8'><em>Nothing to preview.</em></p>";
            IsPreviewVisible = true;
            return;
        }

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            PreviewHtml = await _notesApi.RenderMarkdownAsync(serverUrl, token, Content, ct);
            IsPreviewVisible = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render markdown preview.");
            PreviewHtml = "<p style='color:#EF4444'>Preview failed. Check server connection.</p>";
            IsPreviewVisible = true;
        }
    }

    /// <summary>Toggles between edit and preview mode.</summary>
    [RelayCommand]
    private void TogglePreview()
    {
        if (IsPreviewVisible)
        {
            IsPreviewVisible = false;
        }
        else
        {
            RenderPreviewCommand.Execute(null);
        }
    }

    /// <summary>Navigates back without saving.</summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    // ── Private Helpers ────────────────────────────────────────────

    /// <summary>
    /// Builds the update payload for the current editor state. Selecting "None (unfiled)" sends
    /// <see cref="UpdateNoteDto.ClearFolder"/> because a bare <c>null</c> folder means "no change"
    /// to the server. Folder fields are omitted for a note shared with the caller — folders belong
    /// to the note's owner, so the server would reject a folder the caller owns anyway.
    /// </summary>
    internal UpdateNoteDto BuildUpdateDto() => new()
    {
        Title = Title,
        Content = Content,
        FolderId = CanEditFolder ? SelectedFolderId : null,
        ClearFolder = CanEditFolder && SelectedFolderId is null,
        ExpectedVersion = _currentVersion
    };

    /// <summary>Builds the create payload for a new note, filing it in the selected folder.</summary>
    internal CreateNoteDto BuildCreateDto() => new()
    {
        Title = Title,
        Content = Content,
        FolderId = SelectedFolderId,
        Format = NoteContentFormat.Markdown
    };

    /// <summary>
    /// Loads the caller's folders into the picker. A failure here is not fatal — the picker simply
    /// stays at "None (unfiled)" and the note can still be saved.
    /// </summary>
    private async Task LoadFolderOptionsAsync(CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var folders = await _notesApi.ListFoldersAsync(serverUrl, token, ct: ct);

            FolderOptions.Clear();
            FolderOptions.Add(new NoteFolderOption(null, NoFolderLabel));
            foreach (var folder in folders)
                FolderOptions.Add(new NoteFolderOption(folder.Id, folder.Name));

            if (SelectedFolderId is { } selectedId)
                AddMissingFolderOption(selectedId);

            SyncSelectedFolderOption();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load note folders for the editor picker.");
        }
    }

    /// <summary>
    /// Adds a picker entry for a folder that is not in the caller's folder list — a shared note
    /// keeps its owner's folder, and showing it keeps the note filed when the note is saved.
    /// </summary>
    private void AddMissingFolderOption(Guid folderId)
    {
        if (FolderOptions.All(o => o.Id != folderId))
            FolderOptions.Add(new NoteFolderOption(folderId, NoteFolderLabels.SharedFolderFallback));
    }

    /// <summary>Points the picker at the entry matching <see cref="SelectedFolderId"/>.</summary>
    private void SyncSelectedFolderOption()
    {
        if (_syncingFolderSelection)
            return;

        _syncingFolderSelection = true;
        try
        {
            var match = FolderOptions.FirstOrDefault(o => o.Id == SelectedFolderId);

            // Leave the selection alone when the folder is not listed (yet); never infer "unfiled".
            if (match is not null || SelectedFolderId is null)
                SelectedFolderOption = match;
        }
        finally
        {
            _syncingFolderSelection = false;
        }
    }

    private async Task<(string ServerUrl, string Token)> GetCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
            ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            ?? throw new InvalidOperationException("No access token available.");
        return (connection.ServerBaseUrl, token);
    }
}

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
    }

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

    // ── Commands ───────────────────────────────────────────────────

    /// <summary>
    /// Loads the note for editing. Called when the page appears.
    /// In create mode (no NoteId), this is a no-op.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        if (_loaded)
            return;
        _loaded = true;

        if (!IsEditing || string.IsNullOrEmpty(NoteId))
            return;
        if (!Guid.TryParse(NoteId, out var noteId))
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var note = await _notesApi.GetNoteAsync(serverUrl, token, noteId, ct);
            Title = note.Title;
            Content = note.Content;
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
                        JsonSerializer.Serialize(new OfflineNoteUpdatePayload(offlineNoteId, new UpdateNoteDto
                        {
                            Title = Title,
                            Content = Content,
                            FolderId = SelectedFolderId,
                            ExpectedVersion = _currentVersion
                        })), ct).ConfigureAwait(false);
                }
                else
                {
                    await _offlineQueue.EnqueueAsync(OfflineOperationType.NoteCreate,
                        JsonSerializer.Serialize(new OfflineNoteCreatePayload(new CreateNoteDto
                        {
                            Title = Title,
                            Content = Content,
                            FolderId = SelectedFolderId,
                            Format = NoteContentFormat.Markdown
                        })), ct).ConfigureAwait(false);
                }

                var isNewOffline = !IsEditing;
                await Shell.Current.GoToAsync("..");
                WeakReferenceMessenger.Default.Send(new NoteSavedMessage(isNewOffline));
                return;
            }

            var (serverUrl, token) = await GetCredentialsAsync(ct);

            if (IsEditing && Guid.TryParse(NoteId, out var noteId))
            {
                await _notesApi.UpdateNoteAsync(serverUrl, token, noteId,
                    new UpdateNoteDto
                    {
                        Title = Title,
                        Content = Content,
                        FolderId = SelectedFolderId,
                        ExpectedVersion = _currentVersion
                    }, ct);
            }
            else
            {
                await _notesApi.CreateNoteAsync(serverUrl, token,
                    new CreateNoteDto
                    {
                        Title = Title,
                        Content = Content,
                        FolderId = SelectedFolderId,
                        Format = NoteContentFormat.Markdown
                    }, ct);
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

    private async Task<(string ServerUrl, string Token)> GetCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
            ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            ?? throw new InvalidOperationException("No access token available.");
        return (connection.ServerBaseUrl, token);
    }
}

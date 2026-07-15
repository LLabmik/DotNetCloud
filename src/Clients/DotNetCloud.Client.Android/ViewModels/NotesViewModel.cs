using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>Main ViewModel for the Notes tab.</summary>
public sealed partial class NotesViewModel : ObservableObject
{
    private readonly INotesRestClient _notesApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<NotesViewModel> _logger;

    /// <summary>Initializes a new <see cref="NotesViewModel"/>.</summary>
    public NotesViewModel(
        INotesRestClient notesApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<NotesViewModel> logger)
    {
        _notesApi = notesApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    // ── View State ─────────────────────────────────────────────────

    /// <summary>Whether data is loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Error message to display, or null.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Current search query text.</summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>Currently selected folder ID. Null means "All Notes".</summary>
    [ObservableProperty]
    private Guid? _selectedFolderId;

    /// <summary>The note currently being previewed, or null.</summary>
    [ObservableProperty]
    private NoteDto? _selectedNote;

    /// <summary>Rendered HTML for the note currently being previewed.</summary>
    [ObservableProperty]
    private string _previewHtml = string.Empty;

    /// <summary>Whether the preview panel is visible.</summary>
    [ObservableProperty]
    private bool _isPreviewVisible;

    /// <summary>Whether the page is currently visible. Prevents background load errors from showing after navigating away.</summary>
    internal bool IsActive { get; set; }

    /// <summary>Whether the notes list is empty.</summary>
    public bool IsEmpty => Notes.Count == 0;

    // ── Data Collections ───────────────────────────────────────────

    /// <summary>All notes for the current filter.</summary>
    public ObservableCollection<NoteDto> Notes { get; } = [];

    /// <summary>User's note folders.</summary>
    public ObservableCollection<NoteFolderDto> Folders { get; } = [];

    // ── Computed ───────────────────────────────────────────────────

    /// <summary>Currently selected folder name for display, or "All Notes".</summary>
    public string SelectedFolderName =>
        Folders.FirstOrDefault(f => f.Id == SelectedFolderId)?.Name ?? "All Notes";

    // ── Commands ───────────────────────────────────────────────────

    /// <summary>Loads notes from the server with the current folder and search filters.</summary>
    [RelayCommand]
    private async Task LoadNotesAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);

            IReadOnlyList<NoteDto> notes;
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                notes = await _notesApi.SearchNotesAsync(serverUrl, token, SearchQuery, ct: ct);
            }
            else
            {
                notes = await _notesApi.ListNotesAsync(serverUrl, token, SelectedFolderId, ct: ct);
            }

            Notes.Clear();
            foreach (var n in notes)
                Notes.Add(n);

            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notes.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Loads folders from the server.</summary>
    [RelayCommand]
    private async Task LoadFoldersAsync(CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var folders = await _notesApi.ListFoldersAsync(serverUrl, token, ct: ct);

            Folders.Clear();
            foreach (var f in folders)
                Folders.Add(f);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load folders.");
        }
    }

    /// <summary>Navigates to NoteEditPage to create a new note.</summary>
    [RelayCommand]
    private async Task CreateNoteAsync()
    {
        await Shell.Current.GoToAsync("NoteEdit");
    }

    /// <summary>Navigates to NoteEditPage to edit an existing note.</summary>
    [RelayCommand]
    private async Task EditNoteAsync(NoteDto? note)
    {
        if (note is null)
            return;
        await Shell.Current.GoToAsync($"NoteEdit?NoteId={note.Id}");
    }

    /// <summary>Soft-deletes a note with confirmation dialog.</summary>
    [RelayCommand]
    private async Task DeleteNoteAsync(NoteDto? note)
    {
        if (note is null)
            return;

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Note", $"Delete \"{note.Title}\"?", "Delete", "Cancel");

        if (!confirm)
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _notesApi.DeleteNoteAsync(serverUrl, token, note.Id);
            Notes.Remove(note);

            // Close preview if the deleted note was being previewed
            if (SelectedNote?.Id == note.Id)
                ClosePreview();

            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete note.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Toggles the pin state of a note.</summary>
    [RelayCommand]
    private async Task TogglePinAsync(NoteDto? note)
    {
        if (note is null)
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            var updated = await _notesApi.UpdateNoteAsync(serverUrl, token, note.Id,
                new UpdateNoteDto { IsPinned = !note.IsPinned, ExpectedVersion = note.Version },
                CancellationToken.None);

            var index = Notes.IndexOf(note);
            if (index >= 0)
                Notes[index] = updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle pin.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Toggles the favorite state of a note.</summary>
    [RelayCommand]
    private async Task ToggleFavoriteAsync(NoteDto? note)
    {
        if (note is null)
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            var updated = await _notesApi.UpdateNoteAsync(serverUrl, token, note.Id,
                new UpdateNoteDto { IsFavorite = !note.IsFavorite, ExpectedVersion = note.Version },
                CancellationToken.None);

            var index = Notes.IndexOf(note);
            if (index >= 0)
                Notes[index] = updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle favorite.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Selects a note and loads its rendered HTML preview.</summary>
    [RelayCommand]
    private async Task SelectNoteAsync(NoteDto? note)
    {
        if (note is null)
            return;
        SelectedNote = note;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            var preview = await _notesApi.GetNotePreviewAsync(serverUrl, token, note.Id);
            PreviewHtml = preview.RenderedHtml;
            IsPreviewVisible = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load note preview.");
            PreviewHtml = $"<div style='color:#EF4444;padding:16px'>Failed to load preview: {ex.Message}</div>";
            IsPreviewVisible = true;
        }
    }

    /// <summary>Closes the preview panel and returns to the note list.</summary>
    [RelayCommand]
    private void ClosePreview()
    {
        SelectedNote = null;
        PreviewHtml = string.Empty;
        IsPreviewVisible = false;
    }

    /// <summary>Filters notes by the given folder (null = all notes).</summary>
    [RelayCommand]
    private async Task SelectFolderAsync(Guid? folderId)
    {
        SelectedFolderId = folderId;
        SearchQuery = string.Empty;
        await LoadNotesCommand.ExecuteAsync(null);
        OnPropertyChanged(nameof(SelectedFolderName));
    }

    /// <summary>Executes search — triggers a filtered reload.</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        SelectedFolderId = null;
        ClosePreview();
        await LoadNotesCommand.ExecuteAsync(null);
    }

    /// <summary>Prompts for a folder name and creates it.</summary>
    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        var name = await Shell.Current.DisplayPromptAsync("New Folder", "Folder name:");
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _notesApi.CreateFolderAsync(serverUrl, token,
                new CreateNoteFolderDto { Name = name.Trim() });
            await LoadFoldersCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Prompts for a new name and renames the folder.</summary>
    [RelayCommand]
    private async Task RenameFolderAsync(NoteFolderDto? folder)
    {
        if (folder is null)
            return;

        var name = await Shell.Current.DisplayPromptAsync(
            "Rename Folder", "New name:", initialValue: folder.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _notesApi.UpdateFolderAsync(serverUrl, token, folder.Id,
                new UpdateNoteFolderDto { Name = name.Trim() });
            await LoadFoldersCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename folder.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Deletes a folder with confirmation. Notes in the folder become un-filed.</summary>
    [RelayCommand]
    private async Task DeleteFolderAsync(NoteFolderDto? folder)
    {
        if (folder is null)
            return;

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Folder",
            $"Delete \"{folder.Name}\"? Notes in this folder will become un-filed.",
            "Delete", "Cancel");

        if (!confirm)
            return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _notesApi.DeleteFolderAsync(serverUrl, token, folder.Id);
            Folders.Remove(folder);

            if (SelectedFolderId == folder.Id)
                await SelectFolderCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete folder.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Refreshes both notes and folders (for pull-to-refresh).</summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        await LoadFoldersCommand.ExecuteAsync(ct);
        await LoadNotesCommand.ExecuteAsync(ct);
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

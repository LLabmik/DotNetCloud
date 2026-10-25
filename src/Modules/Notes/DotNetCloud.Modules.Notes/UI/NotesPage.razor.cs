using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Modules.Notes.Services;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Notes.UI;

/// <summary>Code-behind for the <see cref="NotesPage"/> component.</summary>
public partial class NotesPage : ComponentBase
{
    /// <summary>
    /// Optional note ID to open when the page loads (deep-link from the home widget).
    /// </summary>
    [Parameter]
    public string? NoteId { get; set; }

    private Guid? _lastHandledNoteId;

    private bool _isLoading;
    private DncConfirmDialog _confirmDialog = default!;

    private readonly List<NoteDto> _notes = [];
    private readonly List<NoteFolderDto> _folders = [];
    private readonly List<NoteVersionDto> _versions = [];
    private NoteDto? _selectedNote;
    private NoteEditorModel? _editingNote;
    private string _searchQuery = string.Empty;
    private string _activeSection = "all";
    private Guid? _selectedFolderId;
    private bool _showShareDialog;
    private bool _showVersionPanel;
    private List<DncShareEntry> _shareDialogShares = [];
    private bool _isLoadingShareDialogShares;
    private bool _showFolderForm;
    private string _folderName = string.Empty;
    private Guid? _editingFolderId;
    private bool _sidebarCollapsed;

    /// <summary>The current authenticated user ID (Guid.Empty until resolved).</summary>
    private Guid _currentUserId = Guid.Empty;

    /// <summary>Whether the caller owns the note.</summary>
    private bool IsOwner(NoteDto note) => _currentUserId != Guid.Empty && note.OwnerId == _currentUserId;

    /// <summary>
    /// Whether the caller may edit the note: the owner, or a viewer granted ReadWrite
    /// via a user/team share.
    /// </summary>
    private bool CanEdit(NoteDto note) => IsOwner(note) || note.ViewerPermission == NoteSharePermission.ReadWrite;
    /// <summary>
    /// Whether the folder selector should be shown in the editor. Folders belong to a single
    /// owner, and the server rejects filing a note into a folder that isn't the note owner's,
    /// so only the owner (or a brand-new note, which the caller will own) may pick a folder.
    /// </summary>
    private bool CanChangeFolder => _editingNote?.Id is null || (_selectedNote is not null && IsOwner(_selectedNote));
    /// <summary>Resolves the current user ID from the authentication state.</summary>
    private async Task<Guid> GetCurrentUserIdAsync()
    {
        try
        {
            var state = await AuthState.GetAuthenticationStateAsync();
            var user = state.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                return Guid.Empty;
            }

            var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
        }
        catch (Exception)
        {
            return Guid.Empty;
        }
    }

    /// <summary>Permission choices for note shares (ReadOnly / ReadWrite).</summary>
    private static readonly IReadOnlyList<DncSharePermissionOption> NotePermissionOptions =
    [
        new() { Value = "Read", Label = "View only" },
        new() { Value = "ReadWrite", Label = "Can edit" },
    ];


    private static string GetPreview(string content)
    {
        var singleLine = content.ReplaceLineEndings(" ").Trim();
        return singleLine.Length > 80 ? string.Concat(singleLine.AsSpan(0, 80), "…") : singleLine;
    }

    private IEnumerable<NoteDto> VisibleNotes
    {
        get
        {
            IEnumerable<NoteDto> query = _notes;

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                query = query.Where(n =>
                    n.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    n.Content.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = _activeSection switch
            {
                "favorites" => query.Where(n => n.IsFavorite),
                "shared" => query.Where(n => !IsOwner(n)),
                _ => query
            };

            return filtered.OrderByDescending(n => n.UpdatedAt);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _currentUserId = await GetCurrentUserIdAsync();

        try
        {
            var collapsed = await JSRuntime.InvokeAsync<string>("localStorage.getItem", new object?[] { "dotnetcloud.sidebar:notes" });
            if (bool.TryParse(collapsed ?? "false", out var parsed))
            {
                _sidebarCollapsed = parsed;
            }
        }
        catch
        {
            // localStorage unavailable
        }

        await ReloadAsync();
        await HandleNoteDeepLinkAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Handle noteId changes when already on the page (same-page navigation).
        await HandleNoteDeepLinkAsync();
    }

    /// <summary>
    /// Opens the note referenced by the <c>NoteId</c> deep-link parameter when it is a new,
    /// well-formed id. Repeat handling of the same id is guarded, and a bad id never breaks
    /// the page.
    /// </summary>
    private async Task HandleNoteDeepLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(NoteId) || !Guid.TryParse(NoteId, out var noteId) || noteId == _lastHandledNoteId)
        {
            return;
        }

        _lastHandledNoteId = noteId;
        try
        {
            await SelectNoteAsync(noteId);
        }
        catch (Exception)
        {
            // A stale or inaccessible note id must not break the page.
        }
    }

    private async Task ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        await SaveSidebarCollapsedStateAsync();
    }

    private async Task SaveSidebarCollapsedStateAsync()
    {
        try
        {
            await JSRuntime.InvokeAsync<object?>("localStorage.setItem", new object?[] { "dotnetcloud.sidebar:notes",
_sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch
        {
            // localStorage unavailable
        }
    }

    private async Task ReloadAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            _folders.Clear();
            _folders.AddRange(await ApiClient.ListFoldersAsync());

            _notes.Clear();
            _notes.AddRange(await ApiClient.ListNotesAsync(_selectedFolderId));

            if (_selectedNote is not null)
            {
                _selectedNote = _notes.FirstOrDefault(n => n.Id == _selectedNote.Id);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SelectNoteAsync(Guid noteId)
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            _selectedNote = await ApiClient.GetNoteAsync(noteId);
            _editingNote = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BeginCreateNote()
    {
        _selectedNote = null;
        _editingNote = new NoteEditorModel
        {
            Title = "New note",
            // A note created while a folder filter is active starts in that folder.
            FolderSelection = _selectedFolderId?.ToString() ?? string.Empty
        };
    }

    private void BeginEditSelected()
    {
        if (_selectedNote is null)
        {
            return;
        }

        _editingNote = NoteEditorModel.From(_selectedNote);
    }

    private async Task SaveNoteAsync()
    {
        if (_editingNote is null)
        {
            return;
        }

        if (_editingNote.Id is null)
        {
            _selectedNote = await ApiClient.CreateNoteAsync(_editingNote.ToCreateDto());
        }
        else
        {
            _selectedNote = await ApiClient.UpdateNoteAsync(_editingNote.Id.Value, _editingNote.ToUpdateDto());
        }

        _editingNote = null;
        await ReloadAsync();
    }

    private async Task DeleteNoteFromDetailAsync()
    {
        if (_selectedNote is null)
        {
            return;
        }

        if (!await _confirmDialog.ShowAsync($"Delete note '{_selectedNote.Title}'? This action cannot be undone.", "Delete Note"))
        {
            return;
        }

        await ApiClient.DeleteNoteAsync(_selectedNote.Id);
        _selectedNote = null;
        await ReloadAsync();
    }

    private void CancelEdit()
    {
        _editingNote = null;
    }

    private Task HandleEditorChanged(string content)
    {
        if (_editingNote is not null)
        {
            _editingNote.Content = content;
        }

        return Task.CompletedTask;
    }

    private async Task SelectFolderAsync(Guid folderId)
    {
        _selectedFolderId = folderId;
        await ReloadAsync();
    }

    private async Task ClearFolderFilter()
    {
        _selectedFolderId = null;
        await ReloadAsync();
    }

    /// <summary>
    /// Resolves a note's folder to a display label, or <c>null</c> when the note is unfiled.
    /// Folders belong to their owner, so a folder that is not in the caller's own folder list
    /// belongs to whoever shared the note with them.
    /// </summary>
    private string? GetFolderLabel(Guid? folderId)
    {
        if (!folderId.HasValue)
        {
            return null;
        }

        return _folders.FirstOrDefault(f => f.Id == folderId.Value)?.Name ?? "Shared folder";
    }

    /// <summary>Display name of a note's folder, or "Unfiled" when it has none.</summary>
    private string GetFolderName(Guid? folderId) => GetFolderLabel(folderId) ?? "Unfiled";

    /// <summary>
    /// Moves the selected note to the folder chosen in the detail-view picker
    /// (empty selection = unfiled). Folder changes are owner-only, matching the server rule
    /// that a note's folder must belong to the note owner.
    /// </summary>
    private async Task MoveSelectedNoteAsync(ChangeEventArgs args)
    {
        if (_selectedNote is null || !IsOwner(_selectedNote))
        {
            return;
        }

        var raw = args.Value?.ToString();
        Guid? targetFolderId = Guid.TryParse(raw, out var parsed) ? parsed : null;
        if (targetFolderId == _selectedNote.FolderId)
        {
            return;
        }

        _isLoading = true;
        StateHasChanged();

        try
        {
            _selectedNote = await ApiClient.UpdateNoteAsync(_selectedNote.Id, new UpdateNoteDto
            {
                FolderId = targetFolderId,
                ClearFolder = targetFolderId is null
            });

            await ReloadAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (_selectedNote is null)
        {
            return;
        }

        _selectedNote = await ApiClient.UpdateNoteAsync(_selectedNote.Id, new UpdateNoteDto
        {
            IsFavorite = !_selectedNote.IsFavorite
        });

        await ReloadAsync();
    }

    private async Task OpenShareDialogAsync()
    {
        if (_selectedNote is null)
            return;

        _showShareDialog = true;
        _showVersionPanel = false;
        _shareDialogShares = [];
        _isLoadingShareDialogShares = true;
        StateHasChanged();

        try
        {
            await LoadShareDialogEntriesAsync();
        }
        finally
        {
            _isLoadingShareDialogShares = false;
        }
    }

    private void CloseShareDialogAsync()
    {
        _showShareDialog = false;
    }

    private async Task LoadShareDialogEntriesAsync()
    {
        if (_selectedNote is null)
            return;

        var shares = await ApiClient.ListSharesAsync(_selectedNote.Id);
        _shareDialogShares = await MapShareDtosToEntriesAsync(shares);
    }

    private async Task HandleShareCreatedAsync(DncShareCreatedEventArgs args)
    {
        if (_selectedNote is null)
            return;

        var perm = args.Permission == "ReadWrite" ? NoteSharePermission.ReadWrite : NoteSharePermission.ReadOnly;
        Guid? targetUserId = args.ShareType == "Team" ? null : args.TargetId;
        Guid? targetTeamId = args.ShareType == "Team" ? args.TargetId : null;

        var result = await ApiClient.ShareNoteAsync(_selectedNote.Id, targetUserId, targetTeamId, perm);
        if (result is not null)
        {
            await LoadShareDialogEntriesAsync();
        }
    }

    private async Task HandleShareRemovedAsync(Guid shareId)
    {
        await ApiClient.RevokeShareAsync(shareId);
        _shareDialogShares = [.. _shareDialogShares.Where(e => e.ShareId != shareId)];
    }

    private async Task<List<DncShareEntry>> MapShareDtosToEntriesAsync(IReadOnlyList<NoteShareDto> shares)
    {
        var userNames = await ResolveUserNamesAsync(shares);
        var entries = new List<DncShareEntry>(shares.Count);

        foreach (var share in shares)
        {
            var isTeam = share.SharedWithTeamId is not null;
            string recipientName;

            if (isTeam)
            {
                recipientName = await ResolveTeamNameAsync(share.SharedWithTeamId!.Value)
                    ?? $"{share.SharedWithTeamId.Value:N}"[..8];
            }
            else
            {
                recipientName = userNames.TryGetValue(share.SharedWithUserId, out var name)
                    ? name
                    : $"{share.SharedWithUserId:N}"[..8];
            }

            entries.Add(new DncShareEntry
            {
                ShareId = share.Id,
                RecipientName = recipientName,
                RecipientType = isTeam ? "Team" : "User",
                Permission = share.Permission == NoteSharePermission.ReadWrite ? "ReadWrite" : "Read",
                CreatedAt = share.CreatedAt
            });
        }

        return entries;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveUserNamesAsync(IReadOnlyList<NoteShareDto> shares)
    {
        var ids = shares
            .Where(s => s.SharedWithTeamId is null)
            .Select(s => s.SharedWithUserId)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            return await UserDirectory.GetDisplayNamesAsync(ids);
        }
        catch
        {
            return new Dictionary<Guid, string>();
        }
    }

    private async Task<string?> ResolveTeamNameAsync(Guid teamId)
    {
        try
        {
            var team = await TeamDirectory.GetTeamAsync(teamId);
            return team?.Name;
        }
        catch
        {
            return null;
        }
    }

    private async Task ToggleVersionPanel()
    {
        _showVersionPanel = !_showVersionPanel;
        _showShareDialog = false;
        if (_showVersionPanel && _selectedNote is not null)
        {
            _versions.Clear();
            _versions.AddRange(await ApiClient.GetVersionHistoryAsync(_selectedNote.Id));
        }
    }

    private async Task RestoreVersionAsync(Guid versionId)
    {
        if (_selectedNote is null)
        {
            return;
        }

        _selectedNote = await ApiClient.RestoreVersionAsync(_selectedNote.Id, versionId);
        if (_selectedNote is not null)
        {
            _versions.Clear();
            _versions.AddRange(await ApiClient.GetVersionHistoryAsync(_selectedNote.Id));
        }

        await ReloadAsync();
    }

    private void BeginCreateFolder()
    {
        _showFolderForm = true;
        _editingFolderId = null;
        _folderName = string.Empty;
    }

    private void BeginRenameFolder(NoteFolderDto folder)
    {
        _showFolderForm = true;
        _editingFolderId = folder.Id;
        _folderName = folder.Name;
    }

    private async Task SaveFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(_folderName))
        {
            return;
        }

        if (_editingFolderId is Guid folderId)
        {
            await ApiClient.UpdateFolderAsync(folderId, new UpdateNoteFolderDto { Name = _folderName });
        }
        else
        {
            await ApiClient.CreateFolderAsync(new CreateNoteFolderDto { Name = _folderName });
        }

        _showFolderForm = false;
        _folderName = string.Empty;
        _editingFolderId = null;
        await ReloadAsync();
    }

    private async Task DeleteFolderAsync(Guid folderId)
    {
        await ApiClient.DeleteFolderAsync(folderId);
        if (_selectedFolderId == folderId)
        {
            _selectedFolderId = null;
        }

        await ReloadAsync();
    }

    private static IReadOnlyList<CrossModuleLinkRequest> BuildNoteLinkRequests(NoteDto note)
    {
        return note.Links
            .Select(link => new CrossModuleLinkRequest
            {
                LinkType = link.LinkType switch
                {
                    NoteLinkType.Contact => CrossModuleLinkType.Contact,
                    NoteLinkType.CalendarEvent => CrossModuleLinkType.CalendarEvent,
                    NoteLinkType.Note => CrossModuleLinkType.Note,
                    NoteLinkType.File => CrossModuleLinkType.File,
                    _ => CrossModuleLinkType.Note
                },
                TargetId = link.TargetId
            })
            .ToList();
    }

    private sealed class NoteEditorModel
    {
        public Guid? Id { get; set; }

        /// <summary>
        /// Selected folder as a string so the folder &lt;select&gt; binds directly;
        /// empty string means "no folder (unfiled)".
        /// </summary>
        public string FolderSelection { get; set; } = string.Empty;

        /// <summary>The selected folder ID, or <c>null</c> when the note is unfiled.</summary>
        public Guid? FolderId => Guid.TryParse(FolderSelection, out var folderId) ? folderId : null;

        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string TagCsv { get; set; } = string.Empty;

        public static NoteEditorModel From(NoteDto dto) => new()
        {
            Id = dto.Id,
            FolderSelection = dto.FolderId?.ToString() ?? string.Empty,
            Title = dto.Title,
            Content = dto.Content,
            TagCsv = string.Join(",", dto.Tags)
        };

        public CreateNoteDto ToCreateDto() => new()
        {
            FolderId = FolderId,
            Title = Title,
            Content = Content,
            Tags = ParseTags()
        };

        public UpdateNoteDto ToUpdateDto() => new()
        {
            FolderId = FolderId,
            // An empty selection means the user moved the note out of its folder.
            ClearFolder = FolderId is null,
            Title = Title,
            Content = Content,
            Tags = ParseTags()
        };

        private IReadOnlyList<string> ParseTags()
        {
            return TagCsv
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}

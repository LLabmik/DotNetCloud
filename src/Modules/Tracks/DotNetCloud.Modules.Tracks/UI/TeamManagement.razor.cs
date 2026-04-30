using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Team management component for creating, editing, and managing Tracks teams.
/// </summary>
public sealed partial class TeamManagement : ComponentBase
{
    [Inject]
    private Services.ITracksApiClient Api { get; set; } = default!;

    [Inject]
    private IUserDirectory UserDirectory { get; set; } = default!;

    [Inject]
    private BrowserTimeProvider TimeProvider { get; set; } = default!;

    /// <summary>
    /// List of teams to display.
    /// </summary>
    [Parameter, EditorRequired]
    public List<TracksTeamDto> Teams { get; set; } = [];

    /// <summary>
    /// Invoked when the user wants to view a team's boards.
    /// </summary>
    [Parameter]
    public EventCallback<Guid> OnTeamSelected { get; set; }

    /// <summary>
    /// Invoked when team data changes and the parent should refresh.
    /// </summary>
    [Parameter]
    public EventCallback OnRefreshRequested { get; set; }

    private List<TracksTeamDto> _teams = [];
    private Guid? _selectedTeamId;
    private bool _showCreateDialog;
    private bool _showDeleteTeamConfirm;
    private Guid _teamToDeleteId;
    private string? _errorMessage;

    // Create form
    private string _newTeamName = string.Empty;
    private string? _newTeamDescription;

    // Edit form
    private string _editName = string.Empty;
    private string? _editDescription;

    // Add member
    private string _addMemberSearch = string.Empty;
    private string _addMemberRole = "Member";
    private Guid _selectedUserId;
    private IReadOnlyList<UserSearchResult> _searchResults = [];
    private HashSet<Guid> _existingMemberIds = [];
    private List<TeamMemberViewModel> _teamMembers = [];
    private System.Timers.Timer? _searchDebounce;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await TimeProvider.EnsureInitializedAsync();
            StateHasChanged();
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _teams = Teams;
    }

    private async Task ExpandTeamAsync(Guid teamId)
    {
        if (_selectedTeamId == teamId)
        {
            _selectedTeamId = null;
            _existingMemberIds.Clear();
            return;
        }

        _selectedTeamId = teamId;
        var team = _teams.FirstOrDefault(t => t.Id == teamId);
        if (team is not null)
        {
            _editName = team.Name;
            _editDescription = team.Description;
        }

        await LoadTeamMembersAsync(teamId);
    }

    private async Task LoadTeamMembersAsync(Guid teamId)
    {
        try
        {
            var members = await Api.ListTeamMembersAsync(teamId);
            _existingMemberIds = members.Select(m => m.UserId).ToHashSet();

            // Batch-resolve display names
            var userIds = members.Select(m => m.UserId).ToList();
            IReadOnlyDictionary<Guid, string> displayNames;
            try
            {
                displayNames = await UserDirectory.GetDisplayNamesAsync(userIds, CancellationToken.None);
            }
            catch
            {
                displayNames = new Dictionary<Guid, string>();
            }

            _teamMembers = members.Select(m => new TeamMemberViewModel
            {
                UserId = m.UserId,
                DisplayName = displayNames.TryGetValue(m.UserId, out var name) ? name : "Unknown User",
                Role = m.Role,
                AssignedAt = m.AssignedAt
            }).ToList();
        }
        catch
        {
            _existingMemberIds = [];
            _teamMembers = [];
        }
    }

    private async Task CreateTeamAsync()
    {
        if (string.IsNullOrWhiteSpace(_newTeamName)) return;

        try
        {
            var dto = new CreateTracksTeamDto
            {
                Name = _newTeamName.Trim(),
                Description = string.IsNullOrWhiteSpace(_newTeamDescription) ? null : _newTeamDescription.Trim()
            };

            await Api.CreateTeamAsync(dto, CancellationToken.None);
            _showCreateDialog = false;
            _newTeamName = string.Empty;
            _newTeamDescription = null;
            _errorMessage = null;
            await OnRefreshRequested.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to create team: {ex.Message}";
        }
    }

    private async Task UpdateTeamAsync(Guid teamId)
    {
        try
        {
            var dto = new UpdateTracksTeamDto
            {
                Name = string.IsNullOrWhiteSpace(_editName) ? null : _editName.Trim(),
                Description = _editDescription?.Trim()
            };

            await Api.UpdateTeamAsync(teamId, dto, CancellationToken.None);
            _errorMessage = null;
            await OnRefreshRequested.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to update team: {ex.Message}";
        }
    }

    private async Task DeleteTeamAsync(Guid teamId)
    {
        _showDeleteTeamConfirm = false;
        try
        {
            await Api.DeleteTeamAsync(teamId);
            _selectedTeamId = null;
            _errorMessage = null;
            await OnRefreshRequested.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to delete team: {ex.Message}";
        }
    }

    private async Task SearchUsersAsync()
    {
        if (string.IsNullOrWhiteSpace(_addMemberSearch) || _addMemberSearch.Trim().Length < 2)
        {
            _searchResults = [];
            return;
        }

        _searchDebounce?.Stop();
        _searchDebounce?.Dispose();
        _searchDebounce = new System.Timers.Timer(300);
        _searchDebounce.AutoReset = false;
        _searchDebounce.Elapsed += async (_, _) =>
        {
            try
            {
                var results = await UserDirectory.SearchUsersAsync(_addMemberSearch.Trim(), cancellationToken: CancellationToken.None);
                await InvokeAsync(() =>
                {
                    _searchResults = results.Where(r => !_existingMemberIds.Contains(r.Id)).ToList();
                    StateHasChanged();
                });
            }
            catch
            {
                // Silently ignore search failures
            }
        };
        _searchDebounce.Start();
    }

    private void SelectUserForAdd(UserSearchResult user)
    {
        _selectedUserId = user.Id;
        _addMemberSearch = user.DisplayName;
        _searchResults = [];
    }

    private async Task AddMemberAsync(Guid teamId)
    {
        if (_selectedUserId == Guid.Empty)
        {
            _errorMessage = "Please search and select a user first.";
            return;
        }

        if (!Enum.TryParse<TracksTeamMemberRole>(_addMemberRole, out var role))
        {
            role = TracksTeamMemberRole.Member;
        }

        try
        {
            await Api.AddTeamMemberAsync(teamId, new AddTracksTeamMemberDto { UserId = _selectedUserId, Role = role });
            _addMemberSearch = string.Empty;
            _selectedUserId = Guid.Empty;
            _addMemberRole = "Member";
            _searchResults = [];
            _errorMessage = null;
            await LoadTeamMembersAsync(teamId);
            await OnRefreshRequested.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to add member: {ex.Message}";
        }
    }

    private async Task RemoveMemberAsync(Guid teamId, Guid userId)
    {
        try
        {
            await Api.RemoveTeamMemberAsync(teamId, userId, CancellationToken.None);
            _errorMessage = null;
            await LoadTeamMembersAsync(teamId);
            await OnRefreshRequested.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to remove member: {ex.Message}";
        }
    }

    private async Task UpdateMemberRoleAsync(Guid teamId, Guid userId, ChangeEventArgs e)
    {
        if (e.Value is not string roleStr || !Enum.TryParse<TracksTeamMemberRole>(roleStr, out var role))
        {
            return;
        }

        try
        {
            await Api.UpdateTeamMemberRoleAsync(teamId, userId, role, CancellationToken.None);
            _errorMessage = null;
            await LoadTeamMembersAsync(teamId);
            await OnRefreshRequested.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to update role: {ex.Message}";
        }
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }

    /// <summary>
    /// View model for a team member displayed in the UI.
    /// </summary>
    private sealed class TeamMemberViewModel
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public TracksTeamMemberRole Role { get; init; }
        public DateTime AssignedAt { get; init; }
    }
}

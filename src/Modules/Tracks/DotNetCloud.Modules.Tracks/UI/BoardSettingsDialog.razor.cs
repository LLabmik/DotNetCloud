using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Board settings dialog — members, labels, archive, delete.
/// </summary>
public partial class BoardSettingsDialog : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public BoardDto Board { get; set; } = default!;
    [Parameter] public List<TracksTeamDto> Teams { get; set; } = [];
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<BoardDto> OnBoardUpdated { get; set; }
    [Parameter] public EventCallback<Guid> OnBoardDeleted { get; set; }

    private string _title = "";
    private string _description = "";
    private string _color = "";
    private string _addMemberInput = "";
    private string _newLabelTitle = "";
    private string _newLabelColor = "#3b82f6";
    private string _selectedTeamId = "";
    private string _transferError = "";
    private bool _showArchiveConfirm;
    private bool _showDeleteConfirm;

    private static readonly string[] _colors =
    [
        "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6",
        "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1"
    ];

    private static readonly string[] _labelColors =
    [
        "#ef4444", "#f59e0b", "#10b981", "#3b82f6", "#8b5cf6",
        "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1",
        "#64748b", "#475569"
    ];

    protected override void OnParametersSet()
    {
        _title = Board.Title;
        _description = Board.Description ?? "";
        _color = Board.Color ?? "#3b82f6";
    }

    private async Task SaveGeneralAsync()
    {
        var updated = await ApiClient.UpdateBoardAsync(Board.Id, new UpdateBoardDto
        {
            Title = _title.Trim(),
            Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
            Color = _color
        });

        if (updated is not null) await OnBoardUpdated.InvokeAsync(updated);
    }

    private async Task AddMemberAsync()
    {
        if (!Guid.TryParse(_addMemberInput.Trim(), out var userId)) return;
        await ApiClient.AddBoardMemberAsync(Board.Id, userId, BoardMemberRole.Member);
        _addMemberInput = "";
        await RefreshBoardAsync();
    }

    private async Task RemoveMemberAsync(Guid userId)
    {
        await ApiClient.RemoveBoardMemberAsync(Board.Id, userId);
        await RefreshBoardAsync();
    }

    private async Task UpdateMemberRoleAsync(Guid userId, ChangeEventArgs e)
    {
        if (Enum.TryParse<BoardMemberRole>(e.Value?.ToString(), out var role))
        {
            await ApiClient.UpdateBoardMemberRoleAsync(Board.Id, userId, role);
            await RefreshBoardAsync();
        }
    }

    private async Task CreateLabelAsync()
    {
        if (string.IsNullOrWhiteSpace(_newLabelTitle)) return;
        await ApiClient.CreateLabelAsync(Board.Id, new CreateLabelDto
        {
            Title = _newLabelTitle.Trim(),
            Color = _newLabelColor
        });
        _newLabelTitle = "";
        await RefreshBoardAsync();
    }

    private async Task DeleteLabelAsync(Guid labelId)
    {
        await ApiClient.DeleteLabelAsync(Board.Id, labelId);
        await RefreshBoardAsync();
    }

    private void ShowArchiveConfirm() => _showArchiveConfirm = true;
    private void HideArchiveConfirm() => _showArchiveConfirm = false;

    private async Task ArchiveBoardAsync()
    {
        _showArchiveConfirm = false;
        var updated = await ApiClient.UpdateBoardAsync(Board.Id, new UpdateBoardDto
        {
            IsArchived = !Board.IsArchived
        });
        if (updated is not null) await OnBoardUpdated.InvokeAsync(updated);
    }

    private void ShowDeleteConfirm() => _showDeleteConfirm = true;
    private void HideDeleteConfirm() => _showDeleteConfirm = false;

    private async Task DeleteBoardAsync()
    {
        _showDeleteConfirm = false;
        await ApiClient.DeleteBoardAsync(Board.Id);
        await OnBoardDeleted.InvokeAsync(Board.Id);
    }

    private async Task TransferToTeamAsync()
    {
        _transferError = "";
        if (!Guid.TryParse(_selectedTeamId, out var teamId)) return;
        try
        {
            await ApiClient.TransferBoardAsync(Board.Id, teamId);
            _selectedTeamId = "";
            await RefreshBoardAsync();
        }
        catch (HttpRequestException)
        {
            _transferError = "Transfer failed. You must be a board owner and team manager.";
        }
    }

    private async Task MakePersonalAsync()
    {
        _transferError = "";
        try
        {
            await ApiClient.TransferBoardAsync(Board.Id, null);
            await RefreshBoardAsync();
        }
        catch (HttpRequestException)
        {
            _transferError = "Failed to make personal. You must be the board owner.";
        }
    }

    private async Task RefreshBoardAsync()
    {
        var refreshed = await ApiClient.GetBoardAsync(Board.Id);
        if (refreshed is not null) await OnBoardUpdated.InvokeAsync(refreshed);
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name[..1].ToUpperInvariant();
    }

    private static string GetEffectiveRoleLabel(TracksTeamMemberRole role) => role switch
    {
        TracksTeamMemberRole.Owner => "Owner",
        TracksTeamMemberRole.Manager => "Manager",
        TracksTeamMemberRole.Member => "Member",
        _ => "Viewer"
    };
}

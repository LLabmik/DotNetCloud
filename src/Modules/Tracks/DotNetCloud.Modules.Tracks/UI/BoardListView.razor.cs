using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Grid view of all boards the user is a member of, with create board dialog.
/// </summary>
public partial class BoardListView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public List<BoardDto> Boards { get; set; } = [];
    [Parameter, EditorRequired] public List<TracksTeamDto> Teams { get; set; } = [];
    [Parameter] public EventCallback<Guid> OnBoardSelected { get; set; }
    [Parameter] public EventCallback<BoardDto> OnBoardCreated { get; set; }
    [Parameter] public EventCallback<Guid> OnBoardDeleted { get; set; }
    [Parameter] public string? InitialTeamFilter { get; set; }

    private string _searchQuery = "";
    private string _teamFilter = "";
    private string? _appliedInitialFilter;
    private bool _showCreateDialog;
    private bool _isCreating;
    private string _createTeamId = "";
    private BoardMode _createMode = BoardMode.Personal;

    private readonly CreateBoardModel _createModel = new();

    protected override void OnParametersSet()
    {
        if (InitialTeamFilter is not null && InitialTeamFilter != _appliedInitialFilter)
        {
            _teamFilter = InitialTeamFilter;
            _appliedInitialFilter = InitialTeamFilter;
        }
    }

    private static readonly string[] _boardColors =
    [
        "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6",
        "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1"
    ];

    private IReadOnlyList<BoardDto> FilteredBoards
    {
        get
        {
            IEnumerable<BoardDto> filtered = Boards.Where(b => !b.IsDeleted);

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var query = _searchQuery.Trim();
                filtered = filtered.Where(b =>
                    b.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (b.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (_teamFilter == "personal")
            {
                filtered = filtered.Where(b => !b.TeamId.HasValue);
            }
            else if (Guid.TryParse(_teamFilter, out var teamId))
            {
                filtered = filtered.Where(b => b.TeamId == teamId);
            }

            return filtered.ToList();
        }
    }

    private void OpenCreateDialog()
    {
        _createModel.Title = "";
        _createModel.Description = "";
        _createModel.Color = _boardColors[0];
        _createTeamId = "";
        _createMode = BoardMode.Personal;
        _showCreateDialog = true;
    }

    private void CloseCreateDialog() => _showCreateDialog = false;

    private void SetCreateMode(BoardMode mode)
    {
        _createMode = mode;
        if (mode == BoardMode.Personal)
        {
            _createTeamId = "";
        }
    }

    private async Task CreateBoardAsync()
    {
        if (string.IsNullOrWhiteSpace(_createModel.Title)) return;

        _isCreating = true;
        try
        {
            Guid? teamId = Guid.TryParse(_createTeamId, out var tid) ? tid : null;
            var dto = new CreateBoardDto
            {
                Title = _createModel.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(_createModel.Description) ? null : _createModel.Description.Trim(),
                Color = _createModel.Color,
                TeamId = _createMode == BoardMode.Team ? teamId : null,
                Mode = _createMode
            };

            var board = await ApiClient.CreateBoardAsync(dto);
            if (board is not null)
            {
                await OnBoardCreated.InvokeAsync(board);
            }

            _showCreateDialog = false;
        }
        finally
        {
            _isCreating = false;
        }
    }

    private static string TruncateText(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "…";

    private sealed class CreateBoardModel
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Color { get; set; } = "#3b82f6";
    }
}

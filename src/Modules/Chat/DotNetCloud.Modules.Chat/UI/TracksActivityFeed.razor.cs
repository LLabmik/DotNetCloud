using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the Tracks activity feed component.
/// Subscribes to <see cref="ITracksActivitySignalRService"/> and displays a live
/// stream of Tracks board activity inside the Chat sidebar.
/// </summary>
/// <remarks>
/// Gracefully hidden when the Tracks module is not installed — the injected
/// <see cref="ITracksActivitySignalRService"/> defaults to a null-object stub
/// with <see cref="ITracksActivitySignalRService.IsActive"/> returning <c>false</c>.
/// </remarks>
public partial class TracksActivityFeed : ComponentBase, IDisposable
{
    [Inject] private ITracksActivitySignalRService TracksActivity { get; set; } = default!;

    private readonly List<ActivityItem> _activities = [];
    private AssignmentAlert? _assignmentAlert;
    private const int MaxActivityItems = 20;

    /// <summary>Whether the Tracks module is available and delivering events.</summary>
    protected bool IsTracksAvailable => TracksActivity.IsActive;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        TracksActivity.ActivityReceived += OnActivityReceived;
        TracksActivity.CardAssignedToMe += OnCardAssignedToMe;
    }

    private void OnActivityReceived(TracksActivitySignal signal)
    {
        var item = new ActivityItem
        {
            Action = signal.Action,
            BoardId = signal.BoardId,
            Timestamp = signal.Timestamp,
            IsNew = true
        };

        _activities.Insert(0, item);

        // Trim to max items
        while (_activities.Count > MaxActivityItems)
        {
            _activities.RemoveAt(_activities.Count - 1);
        }

        RequestRender();
    }

    private void OnCardAssignedToMe(Guid cardId, Guid boardId, Guid assignedByUserId)
    {
        _assignmentAlert = new AssignmentAlert
        {
            CardId = cardId,
            BoardId = boardId,
            AssignedByUserId = assignedByUserId,
            Timestamp = DateTime.UtcNow
        };

        RequestRender();
    }

    /// <summary>Request a UI re-render if the component is attached to a renderer.</summary>
    private void RequestRender()
    {
        try { _ = InvokeAsync(StateHasChanged); }
        catch (InvalidOperationException) { /* No render handle — unit test or disposed component */ }
    }

    /// <summary>Dismisses the current card assignment alert.</summary>
    protected void DismissAssignment()
    {
        _assignmentAlert = null;
    }

    /// <summary>Returns an icon character for a given Tracks action.</summary>
    protected static string GetActionIcon(string action) => action switch
    {
        "card_created" => "\U0001F4CB",    // clipboard
        "card_moved" => "\u27A1",          // right arrow
        "card_updated" => "\u270F",        // pencil
        "card_deleted" => "\U0001F5D1",    // wastebasket
        "card_assigned" => "\U0001F464",   // bust in silhouette
        "comment_added" => "\U0001F4AC",   // speech bubble
        "sprint_started" => "\U0001F3C3",  // runner
        "sprint_completed" => "\u2705",    // check mark
        "board_created" => "\U0001F4CB",   // clipboard
        "board_deleted" => "\U0001F5D1",   // wastebasket
        _ => "\u2022"                      // bullet
    };

    /// <summary>Returns a human-readable label for a given Tracks action.</summary>
    protected static string GetActionText(string action) => action switch
    {
        "card_created" => "Card created",
        "card_moved" => "Card moved",
        "card_updated" => "Card updated",
        "card_deleted" => "Card deleted",
        "card_assigned" => "Card assigned",
        "comment_added" => "Comment added",
        "sprint_started" => "Sprint started",
        "sprint_completed" => "Sprint completed",
        "board_created" => "Board created",
        "board_deleted" => "Board deleted",
        _ => action.Replace('_', ' ')
    };

    /// <summary>Formats a UTC timestamp as a relative time string.</summary>
    protected static string FormatTime(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;

        if (elapsed.TotalSeconds < 30) return "just now";
        if (elapsed.TotalMinutes < 1) return $"{(int)elapsed.TotalSeconds}s ago";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalDays < 1) return $"{(int)elapsed.TotalHours}h ago";
        return $"{(int)elapsed.TotalDays}d ago";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        TracksActivity.ActivityReceived -= OnActivityReceived;
        TracksActivity.CardAssignedToMe -= OnCardAssignedToMe;
    }

    /// <summary>An activity item displayed in the feed.</summary>
    internal sealed class ActivityItem
    {
        public required string Action { get; init; }
        public required Guid BoardId { get; init; }
        public required DateTime Timestamp { get; init; }
        public bool IsNew { get; set; }
    }

    /// <summary>An assignment alert for the current user.</summary>
    internal sealed class AssignmentAlert
    {
        public required Guid CardId { get; init; }
        public required Guid BoardId { get; init; }
        public required Guid AssignedByUserId { get; init; }
        public required DateTime Timestamp { get; init; }
    }
}

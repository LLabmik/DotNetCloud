using DotNetCloud.Modules.Chat.DTOs;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the call history panel component.
/// Shows past calls with duration, participants, and callback options.
/// </summary>
public partial class CallHistoryPanel : ComponentBase
{
    /// <summary>List of call history entries to display.</summary>
    [Parameter]
    public IReadOnlyList<CallHistoryDto> Calls { get; set; } = [];

    /// <summary>Whether the initial call history is loading.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Whether more history is being loaded.</summary>
    [Parameter]
    public bool IsLoadingMore { get; set; }

    /// <summary>Whether there are more call history records to load.</summary>
    [Parameter]
    public bool HasMore { get; set; }

    /// <summary>Callback when the panel is closed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Callback to load more history.</summary>
    [Parameter]
    public EventCallback OnLoadMore { get; set; }

    /// <summary>Callback when user clicks to call back (passes the media type).</summary>
    [Parameter]
    public EventCallback<string> OnCallBack { get; set; }

    /// <summary>Handles close button click.</summary>
    protected async Task HandleClose()
    {
        await OnClose.InvokeAsync();
    }

    /// <summary>Handles load more button click.</summary>
    protected async Task HandleLoadMore()
    {
        await OnLoadMore.InvokeAsync();
    }

    /// <summary>Handles call back button click.</summary>
    protected async Task HandleCallBack(CallHistoryDto call)
    {
        await OnCallBack.InvokeAsync(call.MediaType);
    }

    /// <summary>Gets CSS class based on call outcome.</summary>
    internal static string GetOutcomeClass(string state)
    {
        return state switch
        {
            "Ended" => "outcome-ended",
            "Missed" => "outcome-missed",
            "Rejected" => "outcome-rejected",
            "Failed" => "outcome-failed",
            _ => "outcome-other"
        };
    }

    /// <summary>Gets the call icon based on media type and outcome.</summary>
    internal static string GetCallIcon(CallHistoryDto call)
    {
        if (call.State is "Missed")
        {
            return "📵";
        }

        if (call.State is "Rejected")
        {
            return "🚫";
        }

        if (call.State is "Failed")
        {
            return "⚠️";
        }

        return call.MediaType == "Video" ? "📹" : "📞";
    }

    /// <summary>Formats the call outcome for display.</summary>
    internal static string FormatOutcome(string state)
    {
        return state switch
        {
            "Ended" => "Completed",
            "Missed" => "Missed",
            "Rejected" => "Rejected",
            "Failed" => "Failed",
            "Active" => "Active",
            "Ringing" => "Ringing",
            _ => state
        };
    }

    /// <summary>Formats call time relative to now.</summary>
    internal static string FormatCallTime(DateTime createdAtUtc)
    {
        var elapsed = DateTime.UtcNow - createdAtUtc;

        if (elapsed.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (elapsed.TotalHours < 1)
        {
            var mins = (int)elapsed.TotalMinutes;
            return $"{mins}m ago";
        }

        if (elapsed.TotalDays < 1)
        {
            var hours = (int)elapsed.TotalHours;
            return $"{hours}h ago";
        }

        if (elapsed.TotalDays < 7)
        {
            var days = (int)elapsed.TotalDays;
            return $"{days}d ago";
        }

        return createdAtUtc.ToString("MMM d, yyyy");
    }

    /// <summary>Formats duration in seconds to a readable string.</summary>
    internal static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return $"{totalSeconds}s";
        }

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;

        if (minutes < 60)
        {
            return seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";
        }

        var hours = minutes / 60;
        minutes %= 60;
        return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
    }
}

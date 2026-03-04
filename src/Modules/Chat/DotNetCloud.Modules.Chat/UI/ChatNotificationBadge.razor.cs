using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the chat notification badge.
/// Displays total unread count with special highlight for mentions.
/// </summary>
public partial class ChatNotificationBadge : ComponentBase
{
    /// <summary>Total unread message count across all channels.</summary>
    [Parameter]
    public int TotalUnread { get; set; }

    /// <summary>Whether any channels have unread mentions.</summary>
    [Parameter]
    public bool HasMentions { get; set; }
}

using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the direct message view component.
/// Provides a streamlined chat view for 1:1 conversations.
/// </summary>
public partial class DirectMessageView : ComponentBase
{
    /// <summary>The other user in the DM conversation.</summary>
    [Parameter]
    public MemberViewModel? OtherUser { get; set; }

    /// <summary>Messages in the conversation.</summary>
    [Parameter]
    public List<MessageViewModel> Messages { get; set; } = [];

    /// <summary>Whether messages are loading.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Whether there are more messages to load.</summary>
    [Parameter]
    public bool HasMoreMessages { get; set; }

    /// <summary>Users currently typing.</summary>
    [Parameter]
    public List<TypingUserViewModel> TypingUsers { get; set; } = [];

    /// <summary>Available members to surface as @mention suggestions.</summary>
    [Parameter]
    public List<MemberViewModel> MentionSuggestions { get; set; } = [];

    /// <summary>Message being replied to.</summary>
    [Parameter]
    public MessageViewModel? ReplyToMessage { get; set; }

    /// <summary>Callback to load more messages.</summary>
    [Parameter]
    public EventCallback OnLoadMore { get; set; }

    /// <summary>Callback when a reaction is toggled.</summary>
    [Parameter]
    public EventCallback<(Guid MessageId, string Emoji)> OnReactionToggle { get; set; }

    /// <summary>Callback when a message is sent.</summary>
    [Parameter]
    public EventCallback<(string Content, Guid? ReplyToMessageId)> OnSend { get; set; }

    /// <summary>Callback to cancel a reply.</summary>
    [Parameter]
    public EventCallback OnCancelReply { get; set; }

    /// <summary>Callback when user starts typing.</summary>
    [Parameter]
    public EventCallback OnTyping { get; set; }

    /// <summary>Callback when the attach button is clicked.</summary>
    [Parameter]
    public EventCallback OnAttach { get; set; }

    /// <summary>Gets the mention suggestions to pass into the composer.</summary>
    protected List<MemberViewModel> ComposerMentionSuggestions => MentionSuggestions.Count > 0
        ? MentionSuggestions
        : OtherUser is null
            ? []
            : [OtherUser];

    /// <summary>Gets initials from a display name.</summary>
    protected static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][..1]}{parts[^1][..1]}".ToUpperInvariant()
        };
    }
}

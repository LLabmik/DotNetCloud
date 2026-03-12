using Microsoft.AspNetCore.Components;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the message list component.
/// Displays messages with reactions, attachments, and typing indicators.
/// </summary>
public partial class MessageList : ComponentBase
{
    private static readonly Regex InlineCodeRegex = new("`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex BoldRegex = new("\\*\\*([^*]+)\\*\\*", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new("\\*([^*]+)\\*", RegexOptions.Compiled);
    private static readonly Regex LinkRegex = new("\\[([^\\]]+)\\]\\((https?://[^)]+)\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private ElementReference _messageListRef;

    /// <summary>The list of messages to display.</summary>
    [Parameter]
    public List<MessageViewModel> Messages { get; set; } = [];

    /// <summary>Whether messages are currently loading.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Error message shown when message loading fails.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Whether there are more messages to load.</summary>
    [Parameter]
    public bool HasMoreMessages { get; set; }

    /// <summary>Whether a channel is currently selected.</summary>
    [Parameter]
    public bool ChannelSelected { get; set; }

    /// <summary>Users currently typing in the channel.</summary>
    [Parameter]
    public List<TypingUserViewModel> TypingUsers { get; set; } = [];

    /// <summary>Callback to load older messages.</summary>
    [Parameter]
    public EventCallback OnLoadMore { get; set; }

    /// <summary>Callback when a reaction is toggled.</summary>
    [Parameter]
    public EventCallback<(Guid MessageId, string Emoji)> OnReactionToggle { get; set; }

    /// <summary>Callback when the user wants to edit a message.</summary>
    [Parameter]
    public EventCallback<MessageViewModel> OnEditMessage { get; set; }

    /// <summary>Callback when the user wants to delete a message.</summary>
    [Parameter]
    public EventCallback<Guid> OnDeleteMessage { get; set; }

    /// <summary>Callback when the user wants to reply to a message.</summary>
    [Parameter]
    public EventCallback<MessageViewModel> OnReplyToMessage { get; set; }

    /// <summary>The current user's ID (for showing edit/delete on own messages).</summary>
    [Parameter]
    public Guid CurrentUserId { get; set; }

    /// <summary>Currently editing message, if any.</summary>
    [Parameter]
    public MessageViewModel? EditingMessage { get; set; }

    /// <summary>
    /// Message ID where the unread/new messages divider should appear.
    /// </summary>
    [Parameter]
    public Guid? NewMessagesStartMessageId { get; set; }

    /// <summary>Reference to the message list element for scrolling.</summary>
    protected ElementReference MessageListRef
    {
        get => _messageListRef;
        set => _messageListRef = value;
    }

    /// <summary>Loads older messages.</summary>
    protected async Task LoadMoreMessages()
    {
        await OnLoadMore.InvokeAsync();
    }

    /// <summary>Toggles a reaction on a message.</summary>
    protected async Task ToggleReaction(Guid messageId, string emoji)
    {
        await OnReactionToggle.InvokeAsync((messageId, emoji));
    }

    /// <summary>Requests editing a message.</summary>
    protected async Task RequestEdit(MessageViewModel message)
    {
        await OnEditMessage.InvokeAsync(message);
    }

    /// <summary>Requests deleting a message.</summary>
    protected async Task RequestDelete(Guid messageId)
    {
        await OnDeleteMessage.InvokeAsync(messageId);
    }

    /// <summary>Requests replying to a message.</summary>
    protected async Task RequestReply(MessageViewModel message)
    {
        await OnReplyToMessage.InvokeAsync(message);
    }

    /// <summary>Gets the initials from a display name for the avatar placeholder.</summary>
    protected static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][..1]}{parts[^1][..1]}".ToUpperInvariant()
        };
    }

    /// <summary>Formats a timestamp for display.</summary>
    protected static string FormatTime(DateTime sentAt)
    {
        var now = DateTime.UtcNow;
        var diff = now - sentAt;

        if (diff.TotalMinutes < 1)
        {
            return "just now";
        }

        if (diff.TotalHours < 1)
        {
            return $"{(int)diff.TotalMinutes}m ago";
        }

        if (sentAt.Date == now.Date)
        {
            return sentAt.ToString("HH:mm");
        }

        if (sentAt.Date == now.Date.AddDays(-1))
        {
            return $"Yesterday {sentAt:HH:mm}";
        }

        return sentAt.ToString("MMM d, HH:mm");
    }

    /// <summary>Formats a file size for display.</summary>
    protected static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };
    }

    /// <summary>Gets the typing indicator text.</summary>
    protected string GetTypingText()
    {
        return TypingUsers.Count switch
        {
            0 => string.Empty,
            1 => $"{TypingUsers[0].DisplayName} is typing...",
            2 => $"{TypingUsers[0].DisplayName} and {TypingUsers[1].DisplayName} are typing...",
            _ => "Several people are typing..."
        };
    }

    /// <summary>
    /// Converts basic markdown markup to safe inline HTML.
    /// </summary>
    protected static MarkupString RenderMarkdown(string? content)
    {
        var encoded = HtmlEncoder.Default.Encode(content ?? string.Empty);

        var html = LinkRegex.Replace(encoded, "<a href=\"$2\" target=\"_blank\" rel=\"noopener noreferrer\">$1</a>");
        html = InlineCodeRegex.Replace(html, "<code>$1</code>");
        html = BoldRegex.Replace(html, "<strong>$1</strong>");
        html = ItalicRegex.Replace(html, "<em>$1</em>");
        html = html.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br />", StringComparison.Ordinal);

        return new MarkupString(html);
    }

    /// <summary>
    /// Returns true when a message attachment should render an inline preview.
    /// </summary>
    protected static bool IsInlinePreviewAttachment(AttachmentViewModel attachment)
    {
        return attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || attachment.MimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            || attachment.MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }
}

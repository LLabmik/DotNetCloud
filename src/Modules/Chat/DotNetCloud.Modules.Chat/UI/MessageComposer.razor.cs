using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the message composer component.
/// Handles message input, send, reply-to, emoji, and attachments.
/// </summary>
public partial class MessageComposer : ComponentBase
{
    private string _messageText = string.Empty;
    private bool _isShowEmojiPicker;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>The channel name for the placeholder text.</summary>
    [Parameter]
    public string? ChannelName { get; set; }

    /// <summary>Message being replied to (null if not replying).</summary>
    [Parameter]
    public MessageViewModel? ReplyToMessage { get; set; }

    /// <summary>Callback when a message is sent.</summary>
    [Parameter]
    public EventCallback<(string Content, Guid? ReplyToMessageId)> OnSend { get; set; }

    /// <summary>Callback when the reply is cancelled.</summary>
    [Parameter]
    public EventCallback OnCancelReply { get; set; }

    /// <summary>Callback when the user starts typing.</summary>
    [Parameter]
    public EventCallback OnTyping { get; set; }

    /// <summary>Callback when the attach button is clicked.</summary>
    [Parameter]
    public EventCallback OnAttach { get; set; }

    /// <summary>Common emoji characters for the quick picker.</summary>
    protected static readonly string[] CommonEmojis =
    [
        "👍", "👎", "😀", "😂", "❤️", "🎉", "🔥", "👀",
        "✅", "❌", "⭐", "🚀", "💡", "📎", "🙏", "👏"
    ];

    /// <summary>Gets or sets the message text.</summary>
    protected string MessageText
    {
        get => _messageText;
        set
        {
            _messageText = value;
            _ = NotifyTyping();
        }
    }

    /// <summary>Whether the send button should be disabled.</summary>
    protected bool IsSendDisabled => string.IsNullOrWhiteSpace(_messageText);

    /// <summary>Whether the emoji picker is visible.</summary>
    protected bool IsShowEmojiPicker => _isShowEmojiPicker;

    /// <summary>Sends the current message.</summary>
    protected async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(_messageText))
        {
            return;
        }

        var content = _messageText.Trim();
        var replyToId = ReplyToMessage?.Id;

        _messageText = string.Empty;
        _isShowEmojiPicker = false;

        await OnSend.InvokeAsync((content, replyToId));
    }

    /// <summary>Handles keyboard events for Enter-to-send.</summary>
    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessage();
        }
    }

    /// <summary>Cancels the reply-to preview.</summary>
    protected async Task CancelReply()
    {
        await OnCancelReply.InvokeAsync();
    }

    /// <summary>Toggles the emoji picker visibility.</summary>
    protected void ToggleEmojiPicker()
    {
        _isShowEmojiPicker = !_isShowEmojiPicker;
    }

    /// <summary>Inserts an emoji into the message text.</summary>
    protected void InsertEmoji(string emoji)
    {
        _messageText += emoji;
        _isShowEmojiPicker = false;
    }

    /// <summary>Wraps the textarea selection with Markdown prefix/suffix via JS interop.</summary>
    protected async Task ApplyFormatAsync(string prefix, string suffix)
    {
        var newValue = await JS.InvokeAsync<string>(
            "composerToolbar.wrapSelection",
            "composer-textarea", prefix, suffix);

        // Keep the C# model in sync with what JS wrote into the DOM.
        _messageText = newValue;
    }

    /// <summary>Handles the attach button click.</summary>
    protected async Task OnAttachClick()
    {
        await OnAttach.InvokeAsync();
    }

    /// <summary>Notifies that the user is typing.</summary>
    private async Task NotifyTyping()
    {
        await OnTyping.InvokeAsync();
    }

    /// <summary>Gets placeholder text for the input area.</summary>
    protected string GetPlaceholder()
    {
        return string.IsNullOrEmpty(ChannelName)
            ? "Type a message..."
            : $"Message #{ChannelName}";
    }

    /// <summary>Truncates content for the reply preview.</summary>
    protected static string TruncateContent(string content)
    {
        const int maxLength = 80;
        return content.Length <= maxLength ? content : string.Concat(content.AsSpan(0, maxLength), "...");
    }
}

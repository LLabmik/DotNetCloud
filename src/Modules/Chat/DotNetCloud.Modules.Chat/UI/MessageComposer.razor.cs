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
    private const int MaxMentionSuggestions = 6;

    private string _messageText = string.Empty;
    private bool _isShowEmojiPicker;
    private int _activeMentionStartIndex = -1;
    private int _activeMentionQueryLength = -1;
    private List<MemberViewModel> _visibleMentionSuggestions = [];

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

    /// <summary>Available members for @mention suggestions.</summary>
    [Parameter]
    public List<MemberViewModel> MentionSuggestions { get; set; } = [];

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
            UpdateMentionAutocomplete();
            _ = NotifyTyping();
        }
    }

    /// <summary>Whether the send button should be disabled.</summary>
    protected bool IsSendDisabled => string.IsNullOrWhiteSpace(_messageText);

    /// <summary>Whether the emoji picker is visible.</summary>
    protected bool IsShowEmojiPicker => _isShowEmojiPicker;

    /// <summary>Whether the mention dropdown is visible.</summary>
    protected bool IsMentionDropdownVisible => _activeMentionStartIndex >= 0 && _visibleMentionSuggestions.Count > 0;

    /// <summary>The currently visible mention suggestions.</summary>
    protected IReadOnlyList<MemberViewModel> VisibleMentionSuggestions => _visibleMentionSuggestions;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        UpdateMentionAutocomplete();
    }

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
        ClearMentionAutocomplete();

        await OnSend.InvokeAsync((content, replyToId));
    }

    /// <summary>Handles keyboard events for Enter-to-send.</summary>
    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && IsMentionDropdownVisible)
        {
            ClearMentionAutocomplete();
            return;
        }

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
        UpdateMentionAutocomplete();
    }

    /// <summary>Wraps the textarea selection with Markdown prefix/suffix via JS interop.</summary>
    protected async Task ApplyFormatAsync(string prefix, string suffix)
    {
        var newValue = await JS.InvokeAsync<string>(
            "composerToolbar.wrapSelection",
            "composer-textarea", prefix, suffix);

        // Keep the C# model in sync with what JS wrote into the DOM.
        _messageText = newValue;
        UpdateMentionAutocomplete();
    }

    /// <summary>Inserts the selected mention into the message text.</summary>
    protected async Task SelectMentionAsync(MemberViewModel member)
    {
        if (_activeMentionStartIndex < 0)
        {
            return;
        }

        var replacement = GetMentionLabel(member);
        var mentionTokenLength = _activeMentionQueryLength + 1;
        var prefix = _messageText[.._activeMentionStartIndex];
        var suffixStart = _activeMentionStartIndex + mentionTokenLength;
        var suffix = suffixStart < _messageText.Length ? _messageText[suffixStart..] : string.Empty;
        var spacer = suffix.Length > 0 && char.IsWhiteSpace(suffix[0]) ? string.Empty : " ";

        _messageText = string.Concat(prefix, replacement, spacer, suffix);
        ClearMentionAutocomplete();

        await NotifyTyping();
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

    /// <summary>Gets the display label for a mention suggestion.</summary>
    protected static string GetMentionLabel(MemberViewModel member)
    {
        return $"@{member.DisplayName}";
    }

    /// <summary>Truncates content for the reply preview.</summary>
    protected static string TruncateContent(string content)
    {
        const int maxLength = 80;
        return content.Length <= maxLength ? content : string.Concat(content.AsSpan(0, maxLength), "...");
    }

    private void UpdateMentionAutocomplete()
    {
        var mentionContext = TryGetMentionContext(_messageText);
        if (mentionContext is null)
        {
            ClearMentionAutocomplete();
            return;
        }

        _activeMentionStartIndex = mentionContext.Value.StartIndex;
        _activeMentionQueryLength = mentionContext.Value.Query.Length;
        _visibleMentionSuggestions = FilterMentionSuggestions(mentionContext.Value.Query);
    }

    private void ClearMentionAutocomplete()
    {
        _activeMentionStartIndex = -1;
        _activeMentionQueryLength = -1;
        _visibleMentionSuggestions = [];
    }

    private List<MemberViewModel> FilterMentionSuggestions(string query)
    {
        IEnumerable<MemberViewModel> candidates = MentionSuggestions
            .Where(member => !string.IsNullOrWhiteSpace(member.DisplayName))
            .GroupBy(member => member.UserId)
            .Select(group => group.First());

        if (!string.IsNullOrWhiteSpace(query))
        {
            candidates = candidates.Where(member => MatchesMentionQuery(member, query));
        }

        return [.. candidates
            .OrderBy(member => GetMentionMatchPriority(member, query))
            .ThenBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxMentionSuggestions)];
    }

    private static bool MatchesMentionQuery(MemberViewModel member, string query)
    {
        return member.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(member.Username)
                && member.Username.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetMentionMatchPriority(MemberViewModel member, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        if (member.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(member.Username)
            && member.Username.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static MentionContext? TryGetMentionContext(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        for (var index = text.Length - 1; index >= 0; index--)
        {
            if (text[index] != '@')
            {
                continue;
            }

            if (index > 0 && !IsMentionBoundary(text[index - 1]))
            {
                continue;
            }

            var query = text[(index + 1)..];
            if (query.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
            {
                return null;
            }

            return new MentionContext(index, query);
        }

        return null;
    }

    private static bool IsMentionBoundary(char character)
    {
        return !char.IsLetterOrDigit(character) && character is not '_' and not '.';
    }

    private readonly record struct MentionContext(int StartIndex, string Query);
}

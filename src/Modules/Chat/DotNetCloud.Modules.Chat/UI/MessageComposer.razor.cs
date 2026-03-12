using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the message composer component.
/// Handles message input, send, reply-to, emoji, and attachments.
/// </summary>
public partial class MessageComposer : ComponentBase, IAsyncDisposable
{
    private const int MaxMentionSuggestions = 6;

    private readonly string _textareaElementId = $"composer-textarea-{Guid.NewGuid():N}";

    private string _messageText = string.Empty;
    private bool _isShowEmojiPicker;
    private int _activeMentionStartIndex = -1;
    private int _activeMentionQueryLength = -1;
    private List<MemberViewModel> _visibleMentionSuggestions = [];
    private DotNetObjectReference<MessageComposer>? _dotNetRef;
    private bool _isPasteHandlerRegistered;
    private Guid _lastEditingMessageId;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>The channel name for the placeholder text.</summary>
    [Parameter]
    public string? ChannelName { get; set; }

    /// <summary>Message being replied to (null if not replying).</summary>
    [Parameter]
    public MessageViewModel? ReplyToMessage { get; set; }

    /// <summary>Message currently being edited (null if not editing).</summary>
    [Parameter]
    public MessageViewModel? EditingMessage { get; set; }

    /// <summary>Callback when a message is sent.</summary>
    [Parameter]
    public EventCallback<(string Content, Guid? ReplyToMessageId)> OnSend { get; set; }

    /// <summary>Callback when an edited message is submitted.</summary>
    [Parameter]
    public EventCallback<(Guid MessageId, string Content)> OnEditSend { get; set; }

    /// <summary>Callback when the reply is cancelled.</summary>
    [Parameter]
    public EventCallback OnCancelReply { get; set; }

    /// <summary>Callback when edit mode is cancelled.</summary>
    [Parameter]
    public EventCallback OnCancelEdit { get; set; }

    /// <summary>Callback when the user starts typing.</summary>
    [Parameter]
    public EventCallback OnTyping { get; set; }

    /// <summary>Callback when the attach button is clicked.</summary>
    [Parameter]
    public EventCallback OnAttach { get; set; }

    /// <summary>Callback when an image is pasted into the composer.</summary>
    [Parameter]
    public EventCallback<PastedImageData> OnPasteImage { get; set; }

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

    /// <summary>Whether the composer is in edit mode.</summary>
    protected bool IsEditMode => EditingMessage is not null;

    /// <summary>Whether the emoji picker is visible.</summary>
    protected bool IsShowEmojiPicker => _isShowEmojiPicker;

    /// <summary>The unique textarea element ID used for JS interop.</summary>
    protected string TextareaElementId => _textareaElementId;

    /// <summary>Whether the mention dropdown is visible.</summary>
    protected bool IsMentionDropdownVisible => _activeMentionStartIndex >= 0 && _visibleMentionSuggestions.Count > 0;

    /// <summary>The currently visible mention suggestions.</summary>
    protected IReadOnlyList<MemberViewModel> VisibleMentionSuggestions => _visibleMentionSuggestions;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // When entering edit mode, populate textarea with the message content
        if (EditingMessage is not null && _lastEditingMessageId != EditingMessage.Id)
        {
            _lastEditingMessageId = EditingMessage.Id;
            _messageText = EditingMessage.Content;
        }
        else if (EditingMessage is null && _lastEditingMessageId != Guid.Empty)
        {
            _lastEditingMessageId = Guid.Empty;
        }

        UpdateMentionAutocomplete();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _isPasteHandlerRegistered)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("composerToolbar.registerPasteImageHandler", _textareaElementId, _dotNetRef);
        _isPasteHandlerRegistered = true;
    }

    /// <summary>Sends the current message or submits an edit.</summary>
    protected async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(_messageText))
        {
            return;
        }

        var content = _messageText.Trim();
        _messageText = string.Empty;
        _isShowEmojiPicker = false;
        ClearMentionAutocomplete();

        if (IsEditMode)
        {
            await OnEditSend.InvokeAsync((EditingMessage!.Id, content));
        }
        else
        {
            var replyToId = ReplyToMessage?.Id;
            await OnSend.InvokeAsync((content, replyToId));
        }
    }

    /// <summary>Handles keyboard events for Enter-to-send.</summary>
    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            if (IsMentionDropdownVisible)
            {
                ClearMentionAutocomplete();
                return;
            }

            if (IsEditMode)
            {
                await CancelEdit();
                return;
            }
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

    /// <summary>Cancels edit mode.</summary>
    protected async Task CancelEdit()
    {
        _messageText = string.Empty;
        _lastEditingMessageId = Guid.Empty;
        await OnCancelEdit.InvokeAsync();
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
            _textareaElementId, prefix, suffix);

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

    /// <summary>
    /// JS callback invoked when an image is pasted into the composer textarea.
    /// </summary>
    [JSInvokable]
    public async Task HandlePastedImageFromJs(string fileName, string contentType, string dataUrl, long sizeBytes)
    {
        await ProcessPastedImageAsync(fileName, contentType, dataUrl, sizeBytes);
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isPasteHandlerRegistered)
        {
            try
            {
                await JS.InvokeVoidAsync("composerToolbar.unregisterPasteImageHandler", _textareaElementId);
            }
            catch (JSDisconnectedException)
            {
                // Browser disconnected during teardown; safe to ignore.
            }
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
        _isPasteHandlerRegistered = false;
    }

    /// <summary>
    /// Processes a pasted-image payload and forwards it to the parent callback.
    /// </summary>
    protected async Task ProcessPastedImageAsync(string fileName, string contentType, string dataUrl, long sizeBytes)
    {
        if (!TryExtractBase64Data(dataUrl, out var base64Data))
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return;
        }

        var normalizedFileName = string.IsNullOrWhiteSpace(fileName)
            ? $"pasted-image-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png"
            : fileName;

        var payload = new PastedImageData
        {
            FileName = normalizedFileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType,
            Data = bytes,
            SizeBytes = sizeBytes > 0 ? sizeBytes : bytes.LongLength
        };

        await OnPasteImage.InvokeAsync(payload);
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

    private static bool TryExtractBase64Data(string dataUrl, out string base64Data)
    {
        base64Data = string.Empty;
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return false;
        }

        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex < 0 || commaIndex == dataUrl.Length - 1)
        {
            return false;
        }

        base64Data = dataUrl[(commaIndex + 1)..];
        return true;
    }

    private readonly record struct MentionContext(int StartIndex, string Query);
}

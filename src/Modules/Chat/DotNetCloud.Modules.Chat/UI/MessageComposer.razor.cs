using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the WYSIWYG message composer component.
/// Handles rich-text input, Markdown conversion, send, reply-to, emoji, and attachments.
/// </summary>
public partial class MessageComposer : ComponentBase, IAsyncDisposable
{
    private const int MaxMentionSuggestions = 6;

    private readonly string _editorElementId = $"wysiwyg-editor-{Guid.NewGuid():N}";

    private string _plainText = string.Empty;
    private bool _isEmpty = true;
    private bool _isShowEmojiPicker;
    private int _activeMentionStartIndex = -1;
    private int _activeMentionQueryLength = -1;
    private List<MemberViewModel> _visibleMentionSuggestions = [];
    private DotNetObjectReference<MessageComposer>? _dotNetRef;
    private bool _isInitialized;
    private Guid _lastEditingMessageId;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>The channel name for the placeholder text.</summary>
    [Parameter]
    public string? ChannelName { get; set; }

    /// <summary>The channel type (Public, Private, DirectMessage, Group).</summary>
    [Parameter]
    public string? ChannelType { get; set; }

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

    /// <summary>The unique editor element ID used for JS interop.</summary>
    protected string EditorElementId => _editorElementId;

    /// <summary>Whether the send button should be disabled.</summary>
    protected bool IsSendDisabled => _isEmpty;

    /// <summary>Whether the composer is in edit mode.</summary>
    protected bool IsEditMode => EditingMessage is not null;

    /// <summary>Whether the emoji picker is visible.</summary>
    protected bool IsShowEmojiPicker => _isShowEmojiPicker;

    /// <summary>Whether the mention dropdown is visible.</summary>
    protected bool IsMentionDropdownVisible => _activeMentionStartIndex >= 0 && _visibleMentionSuggestions.Count > 0;

    /// <summary>The currently visible mention suggestions.</summary>
    protected IReadOnlyList<MemberViewModel> VisibleMentionSuggestions => _visibleMentionSuggestions;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (EditingMessage is not null && _lastEditingMessageId != EditingMessage.Id)
        {
            _lastEditingMessageId = EditingMessage.Id;
            if (_isInitialized)
            {
                await JS.InvokeVoidAsync("wysiwygEditor.setContent", _editorElementId, EditingMessage.Content);
            }
        }
        else if (EditingMessage is null && _lastEditingMessageId != Guid.Empty)
        {
            _lastEditingMessageId = Guid.Empty;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _isInitialized)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("wysiwygEditor.init", _editorElementId, _dotNetRef);
        _isInitialized = true;

        // If edit mode was set before initialisation completed
        if (EditingMessage is not null && _lastEditingMessageId == EditingMessage.Id)
        {
            await JS.InvokeVoidAsync("wysiwygEditor.setContent", _editorElementId, EditingMessage.Content);
        }
    }

    /// <summary>Called from JS when the editor content changes.</summary>
    [JSInvokable]
    public void HandleContentChanged(string plainText, bool isEmpty)
    {
        _plainText = plainText;
        _isEmpty = isEmpty;
        UpdateMentionAutocomplete();

        try
        {
            _ = InvokeAsync(StateHasChanged);
        }
        catch (InvalidOperationException)
        {
            // Render handle not assigned (unit test environment); safe to ignore.
        }

        _ = NotifyTyping();
    }

    /// <summary>Called from JS when Enter is pressed (send).</summary>
    [JSInvokable]
    public async Task HandleEnterKey()
    {
        await SendMessage();
    }

    /// <summary>Called from JS when Escape is pressed.</summary>
    [JSInvokable]
    public async Task HandleEscapeKey()
    {
        if (IsMentionDropdownVisible)
        {
            ClearMentionAutocomplete();
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (IsEditMode)
        {
            await CancelEdit();
        }
    }

    /// <summary>JS callback invoked when an image is pasted.</summary>
    [JSInvokable]
    public async Task HandlePastedImageFromJs(string fileName, string contentType, string dataUrl, long sizeBytes)
    {
        await ProcessPastedImageAsync(fileName, contentType, dataUrl, sizeBytes);
    }

    /// <summary>Applies a WYSIWYG format command via JS interop.</summary>
    protected async Task FormatAsync(string command)
    {
        await JS.InvokeVoidAsync("wysiwygEditor.format", _editorElementId, command);
    }

    /// <summary>Sends the current message or submits an edit.</summary>
    protected async Task SendMessage()
    {
        var content = await JS.InvokeAsync<string>("wysiwygEditor.getMarkdown", _editorElementId);
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        await JS.InvokeVoidAsync("wysiwygEditor.clear", _editorElementId);
        _plainText = string.Empty;
        _isEmpty = true;
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

    /// <summary>Cancels the reply-to preview.</summary>
    protected async Task CancelReply()
    {
        await OnCancelReply.InvokeAsync();
    }

    /// <summary>Cancels edit mode.</summary>
    protected async Task CancelEdit()
    {
        await JS.InvokeVoidAsync("wysiwygEditor.clear", _editorElementId);
        _plainText = string.Empty;
        _isEmpty = true;
        _lastEditingMessageId = Guid.Empty;
        await OnCancelEdit.InvokeAsync();
    }

    /// <summary>Toggles the emoji picker visibility.</summary>
    protected void ToggleEmojiPicker()
    {
        _isShowEmojiPicker = !_isShowEmojiPicker;
    }

    /// <summary>Inserts an emoji at the cursor via JS interop.</summary>
    protected async Task InsertEmoji(string emoji)
    {
        await JS.InvokeVoidAsync("wysiwygEditor.insertText", _editorElementId, emoji);
        _isShowEmojiPicker = false;
    }

    /// <summary>Inserts a mention via JS interop.</summary>
    protected async Task SelectMentionAsync(MemberViewModel member)
    {
        if (_activeMentionStartIndex < 0)
        {
            return;
        }

        var replacement = GetMentionLabel(member);
        await JS.InvokeVoidAsync("wysiwygEditor.insertMention", _editorElementId, replacement, _activeMentionQueryLength);
        ClearMentionAutocomplete();
    }

    /// <summary>Handles the attach button click.</summary>
    protected async Task OnAttachClick()
    {
        await OnAttach.InvokeAsync();
    }

    /// <summary>Gets placeholder text for the editor.</summary>
    protected string GetPlaceholder()
    {
        if (string.IsNullOrEmpty(ChannelName))
            return "Type a message...";

        return ChannelType is "DirectMessage" or "Group"
            ? $"Message @{ChannelName}"
            : $"Message #{ChannelName}";
    }

    /// <summary>Notifies that the user is typing.</summary>
    private async Task NotifyTyping()
    {
        await OnTyping.InvokeAsync();
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
        if (_isInitialized)
        {
            try
            {
                await JS.InvokeVoidAsync("wysiwygEditor.dispose", _editorElementId);
            }
            catch (JSDisconnectedException)
            {
                // Browser disconnected during teardown; safe to ignore.
            }
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
        _isInitialized = false;
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
        var mentionContext = TryGetMentionContext(_plainText);
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

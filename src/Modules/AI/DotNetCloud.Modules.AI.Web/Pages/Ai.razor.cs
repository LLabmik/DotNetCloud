using System.Text.Json;
using System.Text.RegularExpressions;
using DotNetCloud.Modules.AI.Web.Services;
using DotNetCloud.Modules.Sdk.Web;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.AI.Web.Pages;

/// <summary>
/// Code-behind for the AI Assistant module UI.
/// </summary>
/// <remarks>
/// <para>
/// The same single-document chat UI the module shipped as plain JavaScript, ported to Razor
/// components: a conversation sidebar, a streaming message list and a composer. The response is
/// Server-Sent Events, pumped by <c>js/dncModuleFrame.js</c> because <c>HttpClient</c> cannot surface a
/// streaming body in WebAssembly.
/// </para>
/// <para>
/// Assistant answers are Markdown: they go through the shared <see cref="IMarkdownRenderer"/>, which
/// sanitises the HTML, and every link is forced to open outside the frame.
/// </para>
/// </remarks>
public partial class Ai : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan StreamSilenceLimit = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CopiedResetDelay = TimeSpan.FromSeconds(2);

    private readonly List<AiConversationDto> _conversations = [];
    private readonly List<ChatMessage> _messages = [];

    private Guid? _activeId;
    private string _defaultModel = string.Empty;
    private bool _modelHealthy = true;
    private bool _loading;
    private bool _helpOpen;
    private string? _error;

    private bool _streaming;
    private string _pendingText = string.Empty;
    private string _pendingThinking = string.Empty;
    private int? _queuePosition;
    private int? _queueTotal;
    private DateTimeOffset _lastChunkAt;
    private bool _watchdogArmed;
    private CancellationTokenSource? _watchdog;
    private bool _scrollPending;

    private string _input = string.Empty;
    private Guid? _renamingId;
    private string _renameTitle = string.Empty;
    private Guid? _confirmDeleteId;
    private string? _copiedKey;

    private DotNetObjectReference<Ai>? _self;

    [Inject]
    private AiApi Api { get; set; } = default!;

    [Inject]
    private ModuleFrameBridge Bridge { get; set; } = default!;

    [Inject]
    private ModuleStreamClient Stream { get; set; } = default!;

    [Inject]
    private IMarkdownRenderer Markdown { get; set; } = default!;

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    private bool ShowWelcome => !_helpOpen && _activeId is null;

    private bool ShowChat => !_helpOpen && _activeId is not null;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Stream.Received += OnStreamEvent;
        Stream.Failed += OnStreamFailed;
        Stream.Completed += OnStreamCompleted;

        _loading = true;

        try
        {
            _conversations.AddRange(await Api.ListConversationsAsync());
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _error = ex.Message;
        }

        try
        {
            var settings = await Api.GetSettingsAsync();
            _defaultModel = settings?.DefaultModel ?? string.Empty;
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            // A missing settings read only costs the model label.
        }

        _modelHealthy = await Api.IsModelHealthyAsync();
        _loading = false;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Bridge.InitializeAsync();
            _self = DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("dncModuleFrame.bindEnterToSend", "ai-input", _self);
        }

        if (_scrollPending)
        {
            _scrollPending = false;
            await Bridge.ScrollToBottomAsync("ai-messages");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Stream.Received -= OnStreamEvent;
        Stream.Failed -= OnStreamFailed;
        Stream.Completed -= OnStreamCompleted;

        StopWatchdog();
        await Stream.StopAsync();
        _self?.Dispose();
        _self = null;
    }

    /// <summary>Enter in the composer sends the message; Shift+Enter keeps its newline.</summary>
    /// <returns>A task that completes when the message was sent.</returns>
    [JSInvokable]
    public Task OnComposerEnter() => SendAsync();

    // ── Conversations ───────────────────────────────────────

    private async Task NewChatAsync()
    {
        if (_loading || _streaming)
        {
            return;
        }

        _error = null;
        _loading = true;

        try
        {
            var conversation = await Api.CreateConversationAsync("New Chat");
            if (conversation is null)
            {
                _error = "Failed to create the conversation.";
                return;
            }

            _conversations.Insert(0, conversation with { Title = "New Conversation" });
            _activeId = conversation.Id;
            _messages.Clear();
            _scrollPending = true;
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _error = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OpenConversationAsync(Guid conversationId)
    {
        if (_streaming)
        {
            return;
        }

        _error = null;
        _helpOpen = false;

        try
        {
            var detail = await Api.GetConversationAsync(conversationId);
            if (detail is null)
            {
                _error = "Failed to load the conversation.";
                return;
            }

            _activeId = detail.Id;
            _messages.Clear();
            _messages.AddRange(detail.Messages.Select(ToChatMessage));
            _scrollPending = true;
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _error = ex.Message;
        }
    }

    private void StartRenaming(AiConversationDto conversation)
    {
        _renamingId = conversation.Id;
        _renameTitle = conversation.Title;
    }

    private async Task OnRenameKeyDown(KeyboardEventArgs args)
    {
        if (string.Equals(args.Key, "Enter", StringComparison.Ordinal))
        {
            await CommitRenameAsync();
        }
        else if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
        {
            _renamingId = null;
        }
    }

    private async Task CommitRenameAsync()
    {
        if (_renamingId is not { } conversationId)
        {
            return;
        }

        var title = _renameTitle.Trim();
        _renamingId = null;

        var conversation = _conversations.FirstOrDefault(item => item.Id == conversationId);
        if (conversation is null || title.Length == 0 || title == conversation.Title)
        {
            return;
        }

        try
        {
            await Api.RenameConversationAsync(conversationId, title);

            ReplaceConversation(conversationId, item => item with { Title = title });
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _error = ex.Message;
        }
    }

    private async Task DeleteConversationAsync(Guid conversationId)
    {
        _confirmDeleteId = null;

        if (_streaming)
        {
            return;
        }

        try
        {
            await Api.DeleteConversationAsync(conversationId);
            _conversations.RemoveAll(item => item.Id == conversationId);

            if (_activeId == conversationId)
            {
                _activeId = null;
                _messages.Clear();
            }
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _error = ex.Message;
        }
    }

    // ── Streaming ───────────────────────────────────────────

    private async Task SendAsync()
    {
        var text = _input.Trim();
        if (text.Length == 0 || _activeId is not { } conversationId || _streaming)
        {
            return;
        }

        _error = null;
        _pendingText = string.Empty;
        _pendingThinking = string.Empty;
        _queuePosition = null;
        _queueTotal = null;

        _messages.Add(new ChatMessage($"local-{Guid.NewGuid():N}", IsUser: true, text));
        _input = string.Empty;
        _streaming = true;
        _scrollPending = true;

        StartWatchdog();

        try
        {
            await Stream.StartAsync(AiApi.StreamUrl(conversationId), new { message = text });
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            _error = "The assistant is unavailable right now.";
            FinishStreaming();
        }
    }

    private async Task AbortStreamAsync()
    {
        await Stream.StopAsync();
        StopWatchdog();
        _streaming = false;
        _pendingText = string.Empty;
        _pendingThinking = string.Empty;
        _queuePosition = null;
        _queueTotal = null;
        _scrollPending = true;
    }

    private void OnStreamEvent(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload == "[DONE]")
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var status = GetString(root, "status");

            if (string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase))
            {
                _queuePosition = GetInt(root, "position") ?? 1;
                _queueTotal = GetInt(root, "total") ?? 1;
                _lastChunkAt = DateTimeOffset.UtcNow;
                _ = InvokeAsync(StateHasChanged);
                return;
            }

            _queuePosition = null;
            _queueTotal = null;

            var thinking = GetString(root, "thinking");
            if (!string.IsNullOrEmpty(thinking))
            {
                _pendingThinking += thinking;
            }

            var content = GetString(root, "content");
            if (!string.IsNullOrEmpty(content))
            {
                _pendingText += content;

                // The watchdog only arms once real model output arrives: a cold model load can take a
                // minute and must not be mistaken for a hang.
                _watchdogArmed = true;
            }

            if (!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(thinking))
            {
                _lastChunkAt = DateTimeOffset.UtcNow;
                _scrollPending = true;
            }

            _ = InvokeAsync(StateHasChanged);
        }
        catch (JsonException)
        {
            // A malformed chunk is skipped rather than aborting the answer.
        }
    }

    private void OnStreamFailed(int status)
    {
        _error = status is 401 or 403
            ? "Your session has expired. Reload the page and try again."
            : "The assistant is unavailable right now.";

        _ = InvokeAsync(StateHasChanged);
    }

    private void OnStreamCompleted()
    {
        FinishStreaming();
    }

    private void FinishStreaming()
    {
        StopWatchdog();
        _streaming = false;
        _queuePosition = null;
        _queueTotal = null;

        var answer = _pendingText;
        _pendingText = string.Empty;
        _pendingThinking = string.Empty;

        if (answer.Length > 0 && _activeId is { } conversationId)
        {
            _messages.Add(new ChatMessage($"local-{Guid.NewGuid():N}", IsUser: false, answer));

            // The service auto-titles a new conversation from its first user message; mirror that here
            // so the sidebar does not keep showing "New Chat" until the next reload.
            var userMessages = _messages.Where(message => message.IsUser).ToList();
            if (userMessages.Count == 1)
            {
                var preview = userMessages[0].Content;
                var title = preview.Length > 60 ? preview[..60].TrimEnd() + "…" : preview;
                ReplaceConversation(conversationId, item => item with { Title = title });
            }
        }

        _scrollPending = true;
        _ = InvokeAsync(StateHasChanged);
    }

    private void StartWatchdog()
    {
        StopWatchdog();
        _watchdogArmed = false;
        _lastChunkAt = DateTimeOffset.UtcNow;

        var source = new CancellationTokenSource();
        _watchdog = source;
        _ = WatchdogLoopAsync(source.Token);
    }

    private void StopWatchdog()
    {
        _watchdog?.Cancel();
        _watchdog?.Dispose();
        _watchdog = null;
    }

    private async Task WatchdogLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(WatchdogInterval, cancellationToken);

                if (!_streaming)
                {
                    return;
                }

                if (_watchdogArmed && DateTimeOffset.UtcNow - _lastChunkAt > StreamSilenceLimit)
                {
                    _error = "The assistant stopped responding. Try again.";
                    await InvokeAsync(AbortStreamAsync);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping the watchdog is the normal way out.
        }
    }

    // ── Copy ────────────────────────────────────────────────

    private Task CopyMarkdownAsync(ChatMessage message) => CopyAsync(message.Key, "md", message.Content);

    private Task CopyFormattedAsync(ChatMessage message) => CopyAsync(message.Key, "html", message.Content);

    private async Task CopyAsync(string key, string format, string content)
    {
        var copied = format == "md"
            ? await Bridge.CopyTextAsync(content)
            : await Bridge.CopyRenderedAsync($"ai-msg-{key}");

        if (!copied)
        {
            _error = "Copying to the clipboard is not available in this browser.";
            return;
        }

        _copiedKey = $"{key}:{format}";
        StateHasChanged();

        await Task.Delay(CopiedResetDelay);

        if (_copiedKey == $"{key}:{format}")
        {
            _copiedKey = null;
            StateHasChanged();
        }
    }

    // ── Help / helpers ──────────────────────────────────────

    private void OpenHelp()
    {
        _helpOpen = true;
        _scrollPending = true;
    }

    private void CloseHelp()
    {
        _helpOpen = false;
        _scrollPending = true;
    }

    /// <summary>
    /// Renders the assistant's Markdown as sanitised HTML, with every link forced to open in a new tab.
    /// </summary>
    /// <param name="markdown">The model's answer.</param>
    /// <returns>HTML that is safe to place in the page.</returns>
    /// <remarks>
    /// The shared renderer sanitises the markup, so no script or event handler survives. The frame
    /// itself must not navigate — a link click would replace the module UI — so anchors get
    /// <c>target="_blank"</c>; the shell's frame allows popups.
    /// </remarks>
    private string RenderMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var html = Markdown.RenderToHtml(markdown);
        return AnchorTag().Replace(html, "<a target=\"_blank\" rel=\"noopener noreferrer\" ");
    }

    [GeneratedRegex("<a\\s(?![^>]*target=)", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTag();

    private void ReplaceConversation(Guid conversationId, Func<AiConversationDto, AiConversationDto> update)
    {
        for (var index = 0; index < _conversations.Count; index++)
        {
            if (_conversations[index].Id == conversationId)
            {
                _conversations[index] = update(_conversations[index]);
            }
        }
    }

    private static ChatMessage ToChatMessage(AiMessageDto message) =>
        new(message.Id.ToString(), IsUser: string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase), message.Content);

    private static string FormatShortDate(DateTime value)
    {
        if (value == default)
        {
            return string.Empty;
        }

        var local = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
            : value.ToLocalTime();

        return local.ToString("MMM d", System.Globalization.CultureInfo.CurrentCulture);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    private sealed record ChatMessage(string Key, bool IsUser, string Content);
}

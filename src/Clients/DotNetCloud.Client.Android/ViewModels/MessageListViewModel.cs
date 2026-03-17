using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// ViewModel for the message list screen.
/// Loads message history, appends real-time incoming messages, supports sending,
/// emoji insertion, @mention autocomplete, and file attachments.
/// </summary>
public sealed partial class MessageListViewModel : ObservableObject, IDisposable
{
    private readonly IChatRestClient _chatApi;
    private readonly IChatSignalRClient _signalR;
    private readonly ILocalMessageCache _cache;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<MessageListViewModel> _logger;

    private Guid _channelId;
    private string? _serverUrl;
    private string? _accessToken;

    // All channel member names for @mention autocomplete
    private IReadOnlyList<string> _allMemberNames = [];

    // UserId → display name lookup for resolving sender names
    private Dictionary<Guid, string> _memberLookup = [];

    // Typing indicator debounce
    private CancellationTokenSource _typingCts = new();

    /// <summary>Initializes a new <see cref="MessageListViewModel"/>.</summary>
    public MessageListViewModel(
        IChatRestClient chatApi,
        IChatSignalRClient signalR,
        ILocalMessageCache cache,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<MessageListViewModel> logger)
    {
        _chatApi = chatApi;
        _signalR = signalR;
        _cache = cache;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;

        _signalR.OnNewChatMessage += OnNewChatMessage;
    }

    /// <summary>Messages displayed in the list, oldest-first.</summary>
    public ObservableCollection<MessageItemViewModel> Messages { get; } = [];

    /// <summary>@mention autocomplete suggestions (visible when typing @word).</summary>
    public ObservableCollection<string> MentionSuggestions { get; } = [];

    /// <summary>Display name of the current channel.</summary>
    [ObservableProperty]
    private string _channelName = string.Empty;

    /// <summary>Text being composed in the message input.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _composerText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Whether the emoji picker panel is currently open.</summary>
    [ObservableProperty]
    private bool _isEmojiPickerOpen;

    /// <summary>Whether the @mention suggestion list should be shown.</summary>
    [ObservableProperty]
    private bool _showMentionSuggestions;

    /// <summary>Initializes the view model for a specific channel and loads its messages.</summary>
    public async Task InitializeAsync(Guid channelId, string channelName, CancellationToken ct = default)
    {
        _channelId = channelId;
        ChannelName = channelName;

        try
        {
            var connection = _serverStore.GetActive();
            if (connection is null)
            {
                ErrorMessage = "No active server connection.";
                return;
            }
            _serverUrl = connection.ServerBaseUrl;

            _accessToken = await _tokenStore.GetAccessTokenAsync(_serverUrl, ct);
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                ErrorMessage = "No access token found. Please log in again.";
                return;
            }

            // Load members first so we can resolve sender names
            await LoadMemberNamesAsync(ct);

            await LoadMessagesAsync(ct);

            // Join the SignalR broadcast group so we receive real-time messages for this channel
            try
            {
                await _signalR.JoinChannelGroupAsync(channelId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to join SignalR group for channel {ChannelId}; real-time updates may not work.", channelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize message list for channel {ChannelId}.", channelId);
            ErrorMessage = $"Failed to load channel: {ex.Message}";
        }
    }

    private async Task LoadMemberNamesAsync(CancellationToken ct)
    {
        try
        {
            var members = await _chatApi.GetChannelMembersAsync(_serverUrl!, _accessToken!, _channelId, ct);
            _allMemberNames = members.Select(m => m.DisplayName).ToList();
            _memberLookup = members.ToDictionary(m => m.UserId, m => m.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not prefetch member names for @mention.");
        }
    }

    /// <summary>Resolves a sender user ID to a display name using the channel member list.</summary>
    private string ResolveSenderName(Guid senderUserId, string fallbackName)
    {
        if (_memberLookup.TryGetValue(senderUserId, out var displayName))
            return displayName;

        // If the server already provided a name, use it
        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName;

        // Abbreviate the GUID so it's not a giant string
        return senderUserId == Guid.Empty ? "Unknown" : senderUserId.ToString()[..8];
    }

    /// <summary>Loads the message history, falling back to cache if offline.</summary>
    [RelayCommand]
    private async Task LoadMessagesAsync(CancellationToken ct)
    {
        IsLoading = true;
        Messages.Clear();
        IReadOnlyList<CachedMessage> cached = [];

        try
        {
            // Show cached messages while fetching from server
            try
            {
                cached = await _cache.GetRecentAsync(_channelId, ct: ct);
                foreach (var m in cached)
                    Messages.Add(new MessageItemViewModel(m.Id, m.SenderName, m.Content, m.SentAt));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Local message cache unavailable for channel {ChannelId}; loading from server only.", _channelId);
            }

            var messages = await _chatApi.GetMessagesAsync(_serverUrl!, _accessToken!, _channelId, ct: ct);

            Messages.Clear();
            foreach (var m in messages.OrderBy(m => m.SentAt))
            {
                var senderName = ResolveSenderName(m.SenderUserId, m.SenderName);
                Messages.Add(new MessageItemViewModel(m.Id, senderName, m.Content, m.SentAt));
            }

            // Update cache in background
            _ = _cache.UpsertAsync(messages.Select(m => new CachedMessage(m.Id, m.ChannelId, ResolveSenderName(m.SenderUserId, m.SenderName), m.Content, m.SentAt)));

            // Mark latest message as read
            if (messages.Count > 0)
                await _chatApi.MarkReadAsync(_serverUrl!, _accessToken!, _channelId, messages[^1].Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load messages for channel {ChannelId}.", _channelId);
            if (cached.Count == 0)
                ErrorMessage = "Failed to load messages.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Sends the composed message.</summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken ct)
    {
        if (!CanSend()) return;
        var content = ComposerText.Trim();
        ComposerText = string.Empty;
        IsEmojiPickerOpen = false;
        ShowMentionSuggestions = false;
        IsSending = true;

        try
        {
            var message = await _chatApi.SendMessageAsync(_serverUrl!, _accessToken!, _channelId, content, ct);
            Messages.Add(new MessageItemViewModel(message.Id, ResolveSenderName(message.SenderUserId, message.SenderName), message.Content, message.SentAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message.");
            ComposerText = content; // restore on failure
            ErrorMessage = "Failed to send message.";
        }
        finally
        {
            IsSending = false;
        }
    }

    /// <summary>Toggles the emoji picker panel visibility.</summary>
    [RelayCommand]
    private void ToggleEmojiPicker() => IsEmojiPickerOpen = !IsEmojiPickerOpen;

    /// <summary>Inserts an emoji character at the end of the composer text.</summary>
    [RelayCommand]
    private void InsertEmoji(string emoji)
    {
        ComposerText += emoji;
        IsEmojiPickerOpen = false;
    }

    /// <summary>
    /// Completes a @mention by replacing the partial @word at the cursor with the selected name.
    /// </summary>
    [RelayCommand]
    private void SelectMention(string displayName)
    {
        var atIndex = ComposerText.LastIndexOf('@');
        if (atIndex >= 0)
            ComposerText = ComposerText[..atIndex] + $"@{displayName} ";

        ShowMentionSuggestions = false;
        MentionSuggestions.Clear();
    }

    /// <summary>Opens the system media picker and sends the chosen file as a message attachment.</summary>
    [RelayCommand]
    private async Task AttachFileAsync(CancellationToken ct)
    {
        try
        {
            var results = await MediaPicker.Default.PickPhotosAsync();
            if (results is null || !results.Any()) return;

            var result = results.FirstOrDefault();
            if (result is null) return;

            // Send the file name as plain-text message for now;
            // full chunked-upload integration is handled by PhotoAutoUploadService.
            var content = $"📎 {result.FileName}";
            ErrorMessage = null;
            IsSending = true;

            var message = await _chatApi.SendMessageAsync(_serverUrl!, _accessToken!, _channelId, content, ct);
            Messages.Add(new MessageItemViewModel(message.Id, ResolveSenderName(message.SenderUserId, message.SenderName), message.Content, message.SentAt));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach file.");
            ErrorMessage = "Failed to attach file.";
        }
        finally
        {
            IsSending = false;
        }
    }

    partial void OnComposerTextChanged(string value)
    {
        // Debounced typing indicator — fires 500 ms after last keystroke
        _typingCts.Cancel();
        _typingCts = new CancellationTokenSource();
        var token = _typingCts.Token;
        _ = Task.Run(async () =>
        {
            await Task.Delay(500, token).ConfigureAwait(false);
            if (_serverUrl is not null && _accessToken is not null)
                await _chatApi.NotifyTypingAsync(_serverUrl, _accessToken, _channelId, token).ConfigureAwait(false);
        }, token);

        // @mention autocomplete — detect trailing @word
        UpdateMentionSuggestions(value);
    }

    private void UpdateMentionSuggestions(string text)
    {
        var atIndex = text.LastIndexOf('@');
        if (atIndex < 0 || (atIndex > 0 && text[atIndex - 1] != ' ' && atIndex != 0))
        {
            ShowMentionSuggestions = false;
            MentionSuggestions.Clear();
            return;
        }

        var partial = text[(atIndex + 1)..];
        if (partial.Contains(' '))
        {
            ShowMentionSuggestions = false;
            MentionSuggestions.Clear();
            return;
        }

        var matches = _allMemberNames
            .Where(n => n.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        MentionSuggestions.Clear();
        foreach (var m in matches)
            MentionSuggestions.Add(m);

        ShowMentionSuggestions = matches.Count > 0 && partial.Length > 0;
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(ComposerText) && !IsSending;

    /// <summary>Raised when the user wants to view full channel details.</summary>
    public event EventHandler? ViewDetailsRequested;

    /// <summary>Opens the channel details page.</summary>
    [RelayCommand]
    private void ViewDetails() => ViewDetailsRequested?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void Dispose()
    {
        _signalR.OnNewChatMessage -= OnNewChatMessage;
        _typingCts.Dispose();

        // Leave the SignalR broadcast group (best-effort, fire-and-forget)
        if (_channelId != Guid.Empty)
        {
            _ = _signalR.LeaveChannelGroupAsync(_channelId).ContinueWith(
                t => _logger.LogDebug(t.Exception, "Error leaving channel group on dispose."),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private void OnNewChatMessage(object? sender, ChatMessageReceivedEventArgs e)
    {
        if (e.ChannelId != _channelId.ToString()) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var vm = new MessageItemViewModel(e.MessageId, e.SenderDisplayName, e.MessagePreview, new DateTimeOffset(e.SentAt, TimeSpan.Zero));
            Messages.Add(vm);
        });
    }
}

/// <summary>Represents a single message row in the message list.</summary>
public sealed class MessageItemViewModel
{
    /// <summary>Initializes a message list item.</summary>
    public MessageItemViewModel(Guid id, string senderName, string content, DateTimeOffset sentAt)
    {
        Id = id;
        SenderName = senderName;
        Content = content;
        SentAt = sentAt;
    }

    /// <summary>Message identifier.</summary>
    public Guid Id { get; }

    /// <summary>Display name of the sender.</summary>
    public string SenderName { get; }

    /// <summary>First character of the sender name for avatar display.</summary>
    public string SenderInitial => string.IsNullOrEmpty(SenderName) ? "?" : SenderName[..1].ToUpperInvariant();

    /// <summary>Message body text.</summary>
    public string Content { get; }

    /// <summary>When the message was sent (UTC).</summary>
    public DateTimeOffset SentAt { get; }

    /// <summary>Formatted send time for display.</summary>
    public string SentAtDisplay => SentAt.ToLocalTime().ToString("HH:mm");
}

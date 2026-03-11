using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// ViewModel for the message list screen.
/// Loads message history, appends real-time incoming messages, supports sending.
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

    /// <summary>Initializes the view model for a specific channel and loads its messages.</summary>
    public async Task InitializeAsync(Guid channelId, string channelName, CancellationToken ct = default)
    {
        _channelId = channelId;
        ChannelName = channelName;

        var connection = _serverStore.GetActive()
                         ?? throw new InvalidOperationException("No active server connection.");
        _serverUrl = connection.ServerBaseUrl;
        _accessToken = await _tokenStore.GetAccessTokenAsync(_serverUrl, ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("No access token found.");

        await LoadMessagesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Loads the message history, falling back to cache if offline.</summary>
    [RelayCommand]
    private async Task LoadMessagesAsync(CancellationToken ct)
    {
        IsLoading = true;
        Messages.Clear();

        // Show cached messages while fetching
        var cached = await _cache.GetRecentAsync(_channelId, ct: ct).ConfigureAwait(false);
        foreach (var m in cached)
            Messages.Add(new MessageItemViewModel(m.Id, m.SenderName, m.Content, m.SentAt));

        try
        {
            var messages = await _chatApi.GetMessagesAsync(_serverUrl!, _accessToken!, _channelId, ct: ct).ConfigureAwait(false);

            Messages.Clear();
            foreach (var m in messages.OrderBy(m => m.SentAt))
                Messages.Add(new MessageItemViewModel(m.Id, m.SenderName, m.Content, m.SentAt));

            // Update cache in background
            _ = _cache.UpsertAsync(messages.Select(m => new CachedMessage(m.Id, m.ChannelId, m.SenderName, m.Content, m.SentAt))).ConfigureAwait(false);

            // Mark latest message as read
            if (messages.Count > 0)
                await _chatApi.MarkReadAsync(_serverUrl!, _accessToken!, _channelId, messages[^1].Id, ct).ConfigureAwait(false);
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
        IsSending = true;

        try
        {
            var message = await _chatApi.SendMessageAsync(_serverUrl!, _accessToken!, _channelId, content, ct).ConfigureAwait(false);
            Messages.Add(new MessageItemViewModel(message.Id, message.SenderName, message.Content, message.SentAt));
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
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(ComposerText) && !IsSending;

    /// <inheritdoc />
    public void Dispose()
    {
        _signalR.OnNewChatMessage -= OnNewChatMessage;
        _typingCts.Dispose();
    }

    private void OnNewChatMessage(object? sender, ChatMessageReceivedEventArgs e)
    {
        if (e.ChannelId != _channelId.ToString()) return;
        var vm = new MessageItemViewModel(Guid.NewGuid(), e.SenderDisplayName, e.MessagePreview, DateTimeOffset.UtcNow);
        Messages.Add(vm);
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

    /// <summary>Message body text.</summary>
    public string Content { get; }

    /// <summary>When the message was sent (UTC).</summary>
    public DateTimeOffset SentAt { get; }

    /// <summary>Formatted send time for display.</summary>
    public string SentAtDisplay => SentAt.ToLocalTime().ToString("HH:mm");
}

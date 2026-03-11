using System.Windows.Input;
using DotNetCloud.Client.Core.Api;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for the quick-reply popup window shown when the user wants
/// to send a message to a specific chat channel from the desktop tray.
/// </summary>
public sealed class QuickReplyViewModel : ViewModelBase
{
    private readonly string _channelId;
    private readonly string _serverBaseUrl;
    private readonly IChatApiClient _chatApiClient;
    private readonly ILogger<QuickReplyViewModel> _logger;

    private string _messageText = string.Empty;
    private string? _errorMessage;
    private bool _isSending;

    // Typing-indicator debounce: cancelled and replaced on every keystroke.
    private CancellationTokenSource _typingCts = new();

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Raised when the window should close (message sent or cancelled).</summary>
    public event EventHandler? CloseRequested;

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Display name of the target channel.</summary>
    public string ChannelName { get; }

    /// <summary>The message text being composed.</summary>
    public string MessageText
    {
        get => _messageText;
        set
        {
            if (!SetProperty(ref _messageText, value))
                return;

            OnPropertyChanged(nameof(CanSend));
            TriggerTypingIndicator();
        }
    }

    /// <summary>Error message shown when send fails; <c>null</c> when there is no error.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Whether a send operation is currently in flight.</summary>
    public bool IsSending
    {
        get => _isSending;
        private set
        {
            if (SetProperty(ref _isSending, value))
                OnPropertyChanged(nameof(CanSend));
        }
    }

    /// <summary>Whether the Send button should be enabled.</summary>
    public bool CanSend => !string.IsNullOrWhiteSpace(MessageText) && !IsSending;

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Sends the composed message to the channel.</summary>
    public ICommand SendCommand { get; }

    /// <summary>Closes the window without sending.</summary>
    public ICommand CancelCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new <see cref="QuickReplyViewModel"/>.</summary>
    /// <param name="channelId">Identifier of the target channel.</param>
    /// <param name="channelName">Display name of the target channel.</param>
    /// <param name="serverBaseUrl">Base URL of the DotNetCloud server.</param>
    /// <param name="chatApiClient">Chat API client used to send messages.</param>
    /// <param name="logger">Logger instance.</param>
    public QuickReplyViewModel(
        string channelId,
        string channelName,
        string serverBaseUrl,
        IChatApiClient chatApiClient,
        ILogger<QuickReplyViewModel> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverBaseUrl);
        ArgumentNullException.ThrowIfNull(chatApiClient);
        ArgumentNullException.ThrowIfNull(logger);

        _channelId = channelId;
        ChannelName = string.IsNullOrWhiteSpace(channelName) ? "Chat" : channelName;
        _serverBaseUrl = serverBaseUrl;
        _chatApiClient = chatApiClient;
        _logger = logger;

        SendCommand = new AsyncRelayCommand(SendAsync);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    // ── Implementation ────────────────────────────────────────────────────

    private async Task SendAsync()
    {
        if (!CanSend)
            return;

        // Cancel any pending typing-indicator fires.
        await _typingCts.CancelAsync();

        IsSending = true;
        ErrorMessage = null;

        try
        {
            if (!Guid.TryParse(_channelId, out var channelGuid))
            {
                ErrorMessage = "Invalid channel identifier.";
                return;
            }

            await _chatApiClient.SendMessageAsync(_serverBaseUrl, null, channelGuid, MessageText);

            _messageText = string.Empty; // bypass setter / typing-debounce
            OnPropertyChanged(nameof(MessageText));
            OnPropertyChanged(nameof(CanSend));

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quick reply send failed for channel {ChannelId}.", _channelId);
            ErrorMessage = "Failed to send. Please try again.";
        }
        finally
        {
            IsSending = false;
        }
    }

    private void TriggerTypingIndicator()
    {
        if (string.IsNullOrWhiteSpace(MessageText))
            return;

        // Renew the debounce token.
        var cts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _typingCts, cts);
        oldCts.Cancel();
        oldCts.Dispose();

        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), token);

                if (!Guid.TryParse(_channelId, out var channelGuid))
                    return;

                await _chatApiClient.NotifyTypingAsync(_serverBaseUrl, null, channelGuid, token);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a later keystroke — normal, ignore.
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Typing indicator send failed for channel {ChannelId}.", _channelId);
            }
        }, token);
    }
}

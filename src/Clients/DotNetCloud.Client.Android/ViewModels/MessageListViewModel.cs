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
    private Guid _currentUserId;
    private string? _serverUrl;
    private string? _accessToken;

    // All channel member names for @mention autocomplete
    private IReadOnlyList<string> _allMemberNames = [];

    // UserId → display name lookup for resolving sender names
    private Dictionary<Guid, string> _memberLookup = [];

    // Typing indicator debounce
    private CancellationTokenSource _typingCts = new();

    // Pending attachment uploaded to server but waiting for Send button
    private ChatAttachment? _pendingAttachment;

    /// <summary>Whether there's an image uploaded and waiting to be sent with the next message.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _hasPendingAttachment;

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

            // Extract the current user ID from the JWT for own-message detection
            try
            {
                _currentUserId = AccessTokenUserIdExtractor.ExtractUserId(_accessToken);
                _logger.LogInformation("MessageListViewModel: currentUserId={CurrentUserId}", _currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract current user ID from token; own-message detection disabled.");
            }

            // Load members first so we can resolve sender names
            await LoadMemberNamesAsync(ct);

            await LoadMessagesAsync(ct);

            // Ensure SignalR is connected before joining channel groups.
            // The connection may still be establishing via the foreground service.
            try
            {
                await _signalR.ConnectAsync(_serverUrl, _accessToken, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ConnectAsync failed; real-time updates may not be available.");
            }

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
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
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
                var isOwn = m.SenderUserId == _currentUserId;
                Messages.Add(new MessageItemViewModel(m.Id, senderName, m.Content, m.SentAt, isOwn, m.Attachments, _serverUrl));
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
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Sends the composed message, including any pending attachment.</summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken ct)
    {
        if (!CanSend())
            return;

        var content = ComposerText.Trim();
        var attachment = _pendingAttachment;

        // Clear UI state immediately
        ComposerText = string.Empty;
        _pendingAttachment = null;
        HasPendingAttachment = false;
        IsEmojiPickerOpen = false;
        ShowMentionSuggestions = false;
        IsSending = true;

        try
        {
            ChatMessage sentMessage;
            if (attachment is not null)
            {
                var msgContent = string.IsNullOrWhiteSpace(content) ? " " : content;
                sentMessage = await _chatApi.SendMessageWithAttachmentsAsync(
                    _serverUrl!, _accessToken!, _channelId,
                    msgContent, [attachment], ct);
            }
            else
            {
                sentMessage = await _chatApi.SendMessageAsync(_serverUrl!, _accessToken!, _channelId, content, ct);
            }

            // Add the sent message to the UI immediately from the REST response.
            // The SignalR broadcast often arrives before the HTTP response, so the
            // OnNewChatMessage handler may have already added it — dedup by ID.
            var senderName = ResolveSenderName(sentMessage.SenderUserId, sentMessage.SenderName);
            var isOwn = sentMessage.SenderUserId == _currentUserId;
            var vm = new MessageItemViewModel(sentMessage.Id, senderName, sentMessage.Content, sentMessage.SentAt, isOwn, sentMessage.Attachments, _serverUrl);
            if (Messages.All(m => m.Id != sentMessage.Id))
                Messages.Add(vm);

            // Cache the message locally for offline access
            _ = _cache.UpsertAsync([new CachedMessage(sentMessage.Id, sentMessage.ChannelId, senderName, sentMessage.Content, sentMessage.SentAt)]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message.");
            ComposerText = content; // restore on failure
            _pendingAttachment = attachment; // restore pending attachment
            HasPendingAttachment = attachment is not null;
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
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

    /// <summary>
    /// Opens the system media picker, uploads the chosen image to the server,
    /// and stores it as a pending attachment. The attachment is sent when the
    /// user taps Send (↑), along with any typed text.
    /// </summary>
    [RelayCommand]
    private async Task AttachFileAsync(CancellationToken ct)
    {
        try
        {
            var results = await MediaPicker.Default.PickPhotosAsync();
            if (results is null || !results.Any())
                return;

            var result = results.FirstOrDefault();
            if (result is null)
                return;

            ErrorMessage = null;

            // Step 1: Read the picked file into memory
            byte[] fileBytes;
            using (var sourceStream = await result.OpenReadAsync())
            using (var ms = new MemoryStream())
            {
                await sourceStream.CopyToAsync(ms, ct);
                fileBytes = ms.ToArray();
            }

            // Step 2: Upload the image to the server and store as pending
            using var uploadStream = new MemoryStream(fileBytes);
            var uploadResult = await _chatApi.UploadImageAsync(
                _serverUrl!, _accessToken!, _channelId,
                uploadStream, result.FileName, result.ContentType ?? "image/jpeg", ct);

            _pendingAttachment = new ChatAttachment(
                Id: Guid.Empty, // server assigns the actual ID
                FileName: uploadResult.FileName,
                MimeType: uploadResult.MimeType,
                FileSize: uploadResult.FileSize,
                ThumbnailUrl: uploadResult.Url);
            HasPendingAttachment = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach file.");
            ErrorMessage = "Failed to attach file.";
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

    private bool CanSend() => (!string.IsNullOrWhiteSpace(ComposerText) || HasPendingAttachment) && !IsSending;

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
        if (e.ChannelId != _channelId.ToString())
            return;

        // Dispatch to UI thread for ObservableCollection updates.
        // If not on a UI platform (e.g., unit tests), execute inline.
        Action dispatch = () =>
        {
            // Dedup: the sending client already added this message from the HTTP response
            // (see SendAsync/AttachFileAsync). Skip the SignalR echo to avoid duplicates.
            if (Messages.Any(m => m.Id == e.MessageId))
                return;

            // Parse attachments from SignalR payload if present
            IReadOnlyList<ChatAttachment>? attachments = null;
            if (!string.IsNullOrEmpty(e.AttachmentsJson))
            {
                try
                {
                    var dtos = System.Text.Json.JsonSerializer.Deserialize<List<SignalRAttachmentDto>>(e.AttachmentsJson);
                    if (dtos is { Count: > 0 })
                    {
                        attachments = dtos.Select(a => new ChatAttachment(a.Id, a.FileName, a.MimeType, a.FileSize, a.ThumbnailUrl)).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse attachments JSON from SignalR message.");
                }
            }

            var isOwn = e.SenderUserId != Guid.Empty && e.SenderUserId == _currentUserId;
            var vm = new MessageItemViewModel(e.MessageId, e.SenderDisplayName, e.MessagePreview, new DateTimeOffset(e.SentAt, TimeSpan.Zero), isOwn, attachments, _serverUrl);
            Messages.Add(vm);

            // Cache the message locally for offline access
            if (Guid.TryParse(e.ChannelId, out var channelGuid))
            {
                _ = _cache.UpsertAsync([new CachedMessage(e.MessageId, channelGuid, e.SenderDisplayName, e.MessagePreview, new DateTimeOffset(e.SentAt, TimeSpan.Zero))]);
            }
        };

        try
        {
            MainThread.BeginInvokeOnMainThread(dispatch);
        }
        catch
        {
            // Unit test environment without a UI thread — run inline.
            dispatch();
        }
    }
}

/// <summary>Represents a single message row in the message list.</summary>
public sealed class MessageItemViewModel
{
    /// <summary>Initializes a message list item.</summary>
    public MessageItemViewModel(Guid id, string senderName, string content, DateTimeOffset sentAt, bool isOwnMessage = false, IReadOnlyList<ChatAttachment>? attachments = null, string? serverBaseUrl = null)
    {
        Id = id;
        SenderName = senderName;
        Content = content;
        SentAt = sentAt;
        IsOwnMessage = isOwnMessage;
        Attachments = attachments ?? [];
        HasImageAttachment = Attachments.Any(a => a.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));

        // Resolve relative thumbnail URLs against the server base URL (MAUI Image needs absolute)
        var firstImage = Attachments.FirstOrDefault(a => a.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
        if (firstImage?.ThumbnailUrl is not null)
        {
            var url = firstImage.ThumbnailUrl;
            if (url.StartsWith('/') && serverBaseUrl is not null)
            {
                url = serverBaseUrl.TrimEnd('/') + url;
            }
            FirstImageUrl = url;
        }
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

    /// <summary>Whether this message was sent by the current user.</summary>
    public bool IsOwnMessage { get; }

    /// <summary>Attachments on this message.</summary>
    public IReadOnlyList<ChatAttachment> Attachments { get; }

    /// <summary>Whether this message has at least one image attachment.</summary>
    public bool HasImageAttachment { get; }

    /// <summary>Absolute URL of the first image attachment for inline preview.</summary>
    public string? FirstImageUrl { get; }

    /// <summary>Formatted send time for display, matching Blazor's tiered FormatTime.</summary>
    public string SentAtDisplay
    {
        get
        {
            var diff = DateTimeOffset.UtcNow - SentAt;

            // Handle server clock skew: the server sometimes serializes UTC timestamps
            // with a local offset (-05:00 CDT instead of +00:00), making messages
            // appear to be from the future. Re-interpret as UTC in that case.
            var effectiveSent = SentAt;
            if (diff.TotalMinutes < -5)
            {
                // Server sent local time as the timestamp — strip offset, treat as UTC
                effectiveSent = new DateTimeOffset(SentAt.DateTime, TimeSpan.Zero);
                diff = DateTimeOffset.UtcNow - effectiveSent;
            }

            var absMinutes = Math.Abs(diff.TotalMinutes);

            System.Diagnostics.Debug.WriteLine(
                $"SentAtDisplay: absMin={absMinutes:F1}, SentAt={SentAt:O}, effective={effectiveSent:O}, diff={diff.TotalMinutes:F1}");

            if (absMinutes < 1)
                return "just now";

            if (absMinutes < 60)
                return $"{(int)Math.Abs(diff.TotalMinutes)}m ago";

            var tz = TimeZoneInfo.Local;
            var localSent = TimeZoneInfo.ConvertTime(effectiveSent, tz);
            var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

            if (localSent.Date == localNow.Date)
                return localSent.ToString("HH:mm");

            if (localSent.Date == localNow.Date.AddDays(-1))
                return $"Yesterday {localSent:HH:mm}";

            return localSent.ToString("MMM d, HH:mm");
        }
    }
}

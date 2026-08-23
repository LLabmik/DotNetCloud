using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text.Json;
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
    private readonly IOfflineOperationQueue _offlineQueue;
    private readonly IConnectivityMonitor _connectivity;
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

    // ── Pagination state (for infinite scroll) ─────────────────────
    private const int PageSize = 25;
    private int _currentPage = 1;
    private bool _hasMoreMessages = true;

    /// <summary>Whether there's another page of older messages to load.</summary>
    public bool HasMoreMessages => _hasMoreMessages;

    /// <summary>Whether a page load is currently in progress.</summary>
    public bool IsLoadingMore => _isLoadingMore;
    private bool _isLoadingMore;

    // Current search query when in search mode (null = normal mode)
    private string? _activeSearchQuery;

    /// <summary>When set, suppresses the auto-scroll-to-bottom on the next message load.</summary>
    private bool _suppressScrollToBottom;

    /// <summary>Raised after older messages are prepended; carries the first-old message ID for scroll anchor.</summary>
    public event EventHandler<Guid>? OlderMessagesLoaded;

    /// <summary>Raised after the initial message load completes, signaling the view should scroll to the latest message.</summary>
    public event EventHandler? ScrollToBottomRequested;

    /// <summary>Raised after closing search, signaling the view should scroll to the tapped search result message.</summary>
    public event EventHandler<Guid>? ScrollToMessageRequested;

    /// <summary>
    /// Raised after a real-time message is appended to <see cref="Messages"/> (not a duplicate echo).
    /// The view uses this to auto-scroll to the bottom when the user is already near it.
    /// </summary>
    public event EventHandler? NewMessageAdded;

    /// <summary>Whether there's an image uploaded and waiting to be sent with the next message.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _hasPendingAttachment;

    /// <summary>Initializes a new <see cref="MessageListViewModel"/>.</summary>
    public MessageListViewModel(
        IChatRestClient chatApi,
        IChatSignalRClient signalR,
        ILocalMessageCache cache,
        IOfflineOperationQueue offlineQueue,
        IConnectivityMonitor connectivity,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<MessageListViewModel> logger)
    {
        _chatApi = chatApi;
        _signalR = signalR;
        _cache = cache;
        _offlineQueue = offlineQueue;
        _connectivity = connectivity;
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

    /// <summary>Status text shown when a message was queued for offline delivery.</summary>
    [ObservableProperty]
    private string? _queuedStatusText;

    /// <summary>Whether the emoji picker panel is currently open.</summary>
    [ObservableProperty]
    private bool _isEmojiPickerOpen;

    /// <summary>Whether the @mention suggestion list should be shown.</summary>
    [ObservableProperty]
    private bool _showMentionSuggestions;

    // ── Search state ────────────────────────────────────────────────
    private CancellationTokenSource _searchCts = new();

    /// <summary>Whether the inline search panel is open.</summary>
    [ObservableProperty]
    private bool _isSearchOpen;

    /// <summary>Current search query text; changes trigger debounced search.</summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>Whether a search is currently in progress.</summary>
    [ObservableProperty]
    private bool _isSearching;

    /// <summary>Initializes the view model for a specific channel and loads its messages.</summary>
    public async Task InitializeAsync(Guid channelId, string channelName, CancellationToken ct = default)
    {
        _channelId = channelId;
        ChannelName = channelName;

        _logger.LogInformation("InitializeAsync STARTED for channel {ChannelId} ('{ChannelName}')", channelId, channelName);

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

    /// <summary>Loads the first page of message history, falling back to cache if offline.</summary>
    [RelayCommand]
    private async Task LoadMessagesAsync(CancellationToken ct)
    {
        _logger.LogInformation("LoadMessagesAsync ENTERED for channel {ChannelId}, IsLoading={IsLoading}", _channelId, IsLoading);
        IsLoading = true;
        _currentPage = 1;
        _hasMoreMessages = true;
        _activeSearchQuery = null;
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

            var result = await _chatApi.GetMessagesAsync(_serverUrl!, _accessToken!, _channelId, page: 1, pageSize: PageSize, ct: ct);
            _logger.LogInformation("LoadMessagesAsync: got {Count} messages, page {Page}/{TotalPages}, IsLoading={IsLoading}", result.Messages.Count, result.Page, result.TotalPages, IsLoading);

            Messages.Clear();
            // Server returns newest-first; reverse for display (oldest at top, newest at bottom)
            foreach (var m in result.Messages.OrderBy(m => m.SentAt))
            {
                var senderName = ResolveSenderName(m.SenderUserId, m.SenderName);
                var isOwn = m.SenderUserId == _currentUserId;
                Messages.Add(new MessageItemViewModel(m.Id, senderName, m.Content, m.SentAt, isOwn, m.Attachments, _serverUrl));
            }

            _logger.LogInformation("LoadMessagesAsync: Messages.Count={Count} after populate", Messages.Count);

            _hasMoreMessages = result.Page < result.TotalPages;
            OnPropertyChanged(nameof(HasMoreMessages));

            // Update cache in background
            _ = _cache.UpsertAsync(result.Messages.Select(m => new CachedMessage(m.Id, m.ChannelId, ResolveSenderName(m.SenderUserId, m.SenderName), m.Content, m.SentAt)));

            // Mark latest message as read
            if (result.Messages.Count > 0)
                await _chatApi.MarkReadAsync(_serverUrl!, _accessToken!, _channelId, result.Messages[^1].Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load messages for channel {ChannelId}.", _channelId);
            if (cached.Count == 0)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            _logger.LogInformation("LoadMessagesAsync: completed, Messages.Count={Count}, ErrorMessage={ErrorMessage}", Messages.Count, ErrorMessage);
            IsLoading = false;
            // Signal the view to scroll to the latest message after initial load
            if (Messages.Count > 0 && _activeSearchQuery is null && !_suppressScrollToBottom)
            {
                ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            }
            _suppressScrollToBottom = false;
        }
    }

    /// <summary>Loads the next page of older messages and prepends them to the list.</summary>
    [RelayCommand]
    private async Task LoadMoreMessagesAsync(CancellationToken ct)
    {
        if (!_hasMoreMessages || _isLoadingMore || IsLoading)
            return;

        _isLoadingMore = true;
        OnPropertyChanged(nameof(IsLoadingMore));
        ErrorMessage = null;

        try
        {
            var nextPage = _currentPage + 1;

            PagedMessagesResult result;
            if (_activeSearchQuery is not null)
            {
                result = await _chatApi.SearchMessagesAsync(
                    _serverUrl!, _accessToken!, _channelId,
                    _activeSearchQuery, page: nextPage, pageSize: PageSize, ct: ct);
            }
            else
            {
                result = await _chatApi.GetMessagesAsync(
                    _serverUrl!, _accessToken!, _channelId,
                    page: nextPage, pageSize: PageSize, ct: ct);
            }

            _currentPage = nextPage;
            _hasMoreMessages = result.Page < result.TotalPages;
            OnPropertyChanged(nameof(HasMoreMessages));

            // Save the ID of the currently first visible (oldest) message for scroll anchor
            var anchorId = Messages.Count > 0 ? Messages[0].Id : Guid.Empty;

            // Server returns newest-first; we need oldest-first for prepend
            var olderMessages = result.Messages
                .OrderBy(m => m.SentAt)
                .ToList();

            if (olderMessages.Count == 0)
            {
                _hasMoreMessages = false;
                OnPropertyChanged(nameof(HasMoreMessages));
                return;
            }

            // Prepend older messages at the beginning
            var newViewModels = olderMessages.Select(m =>
            {
                var senderName = ResolveSenderName(m.SenderUserId, m.SenderName);
                var isOwn = m.SenderUserId == _currentUserId;
                return new MessageItemViewModel(m.Id, senderName, m.Content, m.SentAt, isOwn, m.Attachments, _serverUrl);
            }).ToList();

            var insertIndex = 0;
            foreach (var vm in newViewModels)
            {
                Messages.Insert(insertIndex, vm);
                insertIndex++;
            }

            // Update cache in background
            _ = _cache.UpsertAsync(olderMessages.Select(m => new CachedMessage(m.Id, m.ChannelId, ResolveSenderName(m.SenderUserId, m.SenderName), m.Content, m.SentAt)));

            // Notify code-behind with the first-oldest message ID for scroll anchor restoration
            if (anchorId != Guid.Empty)
            {
                OlderMessagesLoaded?.Invoke(this, anchorId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load more messages for channel {ChannelId}.", _channelId);
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            _isLoadingMore = false;
            OnPropertyChanged(nameof(IsLoadingMore));
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
            // If the device has no signal, queue plain-text messages for delivery when
            // connectivity returns instead of failing. Attachments require a server upload
            // first, so they can't be queued offline.
            if (!_connectivity.IsOnline)
            {
                if (attachment is not null)
                {
                    ErrorMessage = "Cannot send attachments while offline.";
                    ComposerText = content;
                    _pendingAttachment = attachment;
                    HasPendingAttachment = true;
                }
                else
                {
                    await QueueMessageOfflineAsync(content, ct).ConfigureAwait(false);
                }
                return;
            }

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
            {
                Messages.Add(vm);
                // Own messages also auto-scroll when the user is near the bottom, so the
                // sent message isn't left below the fold (the SignalR echo is deduped).
                NewMessageAdded?.Invoke(this, EventArgs.Empty);
            }

            // Cache the message locally for offline access
            _ = _cache.UpsertAsync([new CachedMessage(sentMessage.Id, sentMessage.ChannelId, senderName, sentMessage.Content, sentMessage.SentAt)]);
        }
        catch (Exception ex) when (IsOfflineException(ex) && !ct.IsCancellationRequested)
        {
            // Network dropped mid-send — queue the plain-text message for later delivery.
            if (attachment is not null)
            {
                _logger.LogWarning(ex, "Attachment send failed while going offline; restoring composer.");
                ComposerText = content;
                _pendingAttachment = attachment;
                HasPendingAttachment = true;
                ErrorMessage = "Network lost while sending attachment. Please try again.";
            }
            else
            {
                _logger.LogWarning(ex, "Send failed due to connectivity; queueing message for offline delivery.");
                await QueueMessageOfflineAsync(content, CancellationToken.None).ConfigureAwait(false);
            }
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

    /// <summary>
    /// Persists a plain-text message to the offline operation queue and shows it locally
    /// so the user sees their message was accepted despite being offline.
    /// </summary>
    private async Task QueueMessageOfflineAsync(string content, CancellationToken ct)
    {
        var payload = new OfflineChatMessagePayload(_channelId, content);
        await _offlineQueue.EnqueueAsync(
            OfflineOperationType.ChatMessage,
            JsonSerializer.Serialize(payload),
            ct).ConfigureAwait(false);

        var localId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var senderName = ResolveSenderName(_currentUserId, string.Empty);

        Messages.Add(new MessageItemViewModel(localId, senderName, content, now, true, null, _serverUrl));
        NewMessageAdded?.Invoke(this, EventArgs.Empty);
        _ = _cache.UpsertAsync([new CachedMessage(localId, _channelId, senderName, content, now)]);

        QueuedStatusText = "Message queued — will send when you're back online.";
        _logger.LogInformation("Queued chat message for offline delivery to channel {ChannelId}.", _channelId);
    }

    /// <summary>Returns true when the exception indicates a loss of connectivity.</summary>
    private static bool IsOfflineException(Exception ex) =>
        ex is HttpRequestException
        or IOException
        or SocketException
        or OperationCanceledException;

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

            // Log the raw filename from MediaPicker for diagnostic purposes
            var rawFileName = result.FileName;
            _logger.LogDebug("MediaPicker returned FileName: {FileName}", rawFileName);

            // Defensive sanitization: if the filename contains a comma + space (possible
            // duplication from MediaPicker on some Android versions), take only the first part.
            // See handoff notes for full investigation context.
            var sanitizedFileName = rawFileName;
            var commaIdx = sanitizedFileName.IndexOf(", ", StringComparison.Ordinal);
            if (commaIdx > 0)
            {
                sanitizedFileName = sanitizedFileName[..commaIdx];
                _logger.LogWarning("Sanitized duplicated filename: {Original} -> {Sanitized}", rawFileName, sanitizedFileName);
            }

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
                uploadStream, sanitizedFileName, result.ContentType ?? "image/jpeg", ct);

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

    /// <summary>
    /// Opens the full-screen image viewer for a chat message containing image attachments.
    /// Passes the image URLs as pipe-separated strings through Shell navigation query parameters.
    /// </summary>
    [RelayCommand]
    private async Task OpenChatImageAsync(MessageItemViewModel? message)
    {
        if (message is null || !message.HasImageAttachment)
            return;

        var imageAttachments = message.Attachments
            .Where(a => a.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (imageAttachments.Count == 0)
            return;

        // Build absolute URLs if needed (thumbnail URLs may be relative)
        var urls = string.Join("|", imageAttachments.Select(a =>
        {
            var url = a.ThumbnailUrl ?? string.Empty;
            if (url.StartsWith('/') && _serverUrl is not null)
                url = _serverUrl.TrimEnd('/') + url;
            return url;
        }));

        var names = string.Join("|", imageAttachments.Select(a => a.FileName));

        // Navigate to the simplified chat image viewer (no CarouselView).
        // Only pass the first image URL — the viewer displays a single image
        // with pinch-to-zoom, no swipe-between-images complexity.
        await Shell.Current.GoToAsync("ChatImageViewer", new Dictionary<string, object>
        {
            ["ImageUrl"] = urls.Split('|')[0],
            ["FileName"] = names.Split('|')[0],
        });
    }

    // ── Search commands ──────────────────────────────────────────────

    /// <summary>Toggles the search panel open/closed.</summary>
    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchOpen = !IsSearchOpen;
        if (!IsSearchOpen)
        {
            // Closing search without a query — just restore normal messages
            if (_activeSearchQuery is not null)
            {
                _searchCts.Cancel();
                _searchCts = new CancellationTokenSource();
                _activeSearchQuery = null;
                SearchQuery = string.Empty;
                _ = LoadMessagesAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>Closes search and scrolls to the specified message in the full message list.</summary>
    [RelayCommand]
    private async Task GoToMessageAsync(MessageItemViewModel? message)
    {
        if (message is null || _activeSearchQuery is null)
            return;

        var targetId = message.Id;

        try
        {
            _suppressScrollToBottom = true;
            await CloseSearchAsync();

            // If the target message isn't in the first page, load more pages until we find it
            await LoadPagesUntilMessageFoundAsync(targetId, CancellationToken.None);

            ScrollToMessageRequested?.Invoke(this, targetId);
        }
        finally
        {
            _suppressScrollToBottom = false;
        }
    }

    /// <summary>Loads additional pages until the target message is found or all pages are exhausted.</summary>
    private async Task LoadPagesUntilMessageFoundAsync(Guid targetId, CancellationToken ct)
    {
        // Quick check — already in the current page
        if (Messages.Any(m => m.Id == targetId))
            return;

        while (_hasMoreMessages)
        {
            var nextPage = _currentPage + 1;

            PagedMessagesResult result;
            try
            {
                result = await _chatApi.GetMessagesAsync(
                    _serverUrl!, _accessToken!, _channelId,
                    page: nextPage, pageSize: PageSize, ct: ct);
            }
            catch
            {
                // If a page load fails, stop trying
                break;
            }

            _currentPage = nextPage;
            _hasMoreMessages = result.Page < result.TotalPages;
            OnPropertyChanged(nameof(HasMoreMessages));

            var olderMessages = result.Messages
                .OrderBy(m => m.SentAt)
                .ToList();

            if (olderMessages.Count == 0)
            {
                _hasMoreMessages = false;
                return;
            }

            // Prepend at the beginning (messages arrive newest-first, oldest-first for display)
            var insertIndex = 0;
            foreach (var m in olderMessages)
            {
                var senderName = ResolveSenderName(m.SenderUserId, m.SenderName);
                var isOwn = m.SenderUserId == _currentUserId;
                Messages.Insert(insertIndex, new MessageItemViewModel(
                    m.Id, senderName, m.Content, m.SentAt, isOwn, m.Attachments, _serverUrl));
                insertIndex++;
            }

            // Check if the target message was in this batch
            if (olderMessages.Any(m => m.Id == targetId))
                return;
        }
    }

    /// <summary>Closes the search panel and restores the normal message list.</summary>
    [RelayCommand]
    private async Task CloseSearchAsync()
    {
        IsSearchOpen = false;
        _activeSearchQuery = null;
        SearchQuery = string.Empty;
        _searchCts.Cancel();
        _searchCts = new CancellationTokenSource();
        await LoadMessagesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Called when <see cref="SearchQuery"/> changes.
    /// Debounces by 300ms before executing the search.
    /// </summary>
    partial void OnSearchQueryChanged(string value)
    {
        _searchCts.Cancel();
        _searchCts = new CancellationTokenSource();

        if (string.IsNullOrWhiteSpace(value))
        {
            // Search text cleared — restore normal messages
            if (_activeSearchQuery is not null)
            {
                _activeSearchQuery = null;
                _ = LoadMessagesAsync(CancellationToken.None);
            }
            return;
        }

        var token = _searchCts.Token;
        var query = value;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    await ExecuteSearchAsync(query, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled — a newer keystroke is pending
            }
        }, token);
    }

    /// <summary>Executes the search against the server API and replaces the message list with results.</summary>
    private async Task ExecuteSearchAsync(string query, CancellationToken ct)
    {
        if (_serverUrl is null || _accessToken is null)
            return;

        IsSearching = true;
        _currentPage = 1;
        _activeSearchQuery = query;

        try
        {
            var result = await _chatApi.SearchMessagesAsync(
                _serverUrl, _accessToken, _channelId,
                query, page: 1, pageSize: PageSize, ct: ct);

            _hasMoreMessages = result.Page < result.TotalPages;
            OnPropertyChanged(nameof(HasMoreMessages));

            // Replace message list with search results, newest-first → oldest-first for display
            Action dispatch = () =>
            {
                Messages.Clear();
                foreach (var m in result.Messages.OrderBy(m => m.SentAt))
                {
                    var senderName = ResolveSenderName(m.SenderUserId, m.SenderName);
                    var isOwn = m.SenderUserId == _currentUserId;
                    Messages.Add(new MessageItemViewModel(
                        m.Id, senderName, m.Content, m.SentAt, isOwn,
                        m.Attachments, _serverUrl, searchQuery: query));
                }

                if (result.Messages.Count == 0)
                {
                    ErrorMessage = "No messages found.";
                }
                else
                {
                    ErrorMessage = null;
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
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query '{Query}' in channel {ChannelId}.", query, _channelId);
            Action errorDispatch = () => ErrorMessage = $"Search failed: {ex.Message}";
            try
            {
                MainThread.BeginInvokeOnMainThread(errorDispatch);
            }
            catch
            {
                errorDispatch();
            }
        }
        finally
        {
            Action finallyDispatch = () => IsSearching = false;
            try
            {
                MainThread.BeginInvokeOnMainThread(finallyDispatch);
            }
            catch
            {
                finallyDispatch();
            }
        }
    }

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

            // Signal the view that a new real-time message arrived so it can auto-scroll
            // to the bottom if the user is already near it.
            NewMessageAdded?.Invoke(this, EventArgs.Empty);

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
    public MessageItemViewModel(Guid id, string senderName, string content, DateTimeOffset sentAt, bool isOwnMessage = false, IReadOnlyList<ChatAttachment>? attachments = null, string? serverBaseUrl = null, string? searchQuery = null)
    {
        Id = id;
        SenderName = senderName;
        Content = content;
        SentAt = sentAt;
        IsOwnMessage = isOwnMessage;
        Attachments = attachments ?? [];
        HasImageAttachment = Attachments.Any(a => a.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));

        // Build highlighted FormattedString if a search query is active
        if (!string.IsNullOrEmpty(searchQuery) && content.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
        {
            HighlightedContent = BuildHighlightedString(content, searchQuery);
        }
        else
        {
            HighlightedContent = null;
        }

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

    /// <summary>
    /// Builds a <see cref="FormattedString"/> that highlights all case-insensitive occurrences
    /// of <paramref name="query"/> within <paramref name="text"/> using a yellow span.
    /// </summary>
    private static FormattedString BuildHighlightedString(string text, string query)
    {
        var formatted = new FormattedString();
        var index = 0;

        while (index < text.Length)
        {
            // Find the next occurrence (case-insensitive)
            var searchIdx = text.IndexOf(query, index, StringComparison.OrdinalIgnoreCase);
            if (searchIdx < 0)
            {
                // No more matches — append remaining text as plain
                formatted.Spans.Add(new Span { Text = text[index..] });
                break;
            }

            // Text before the match
            if (searchIdx > index)
            {
                formatted.Spans.Add(new Span { Text = text[index..searchIdx] });
            }

            // The matched text — highlighted
            formatted.Spans.Add(new Span
            {
                Text = text[searchIdx..(searchIdx + query.Length)],
                BackgroundColor = Color.FromArgb("#F59E0B"),
                TextColor = Color.FromArgb("#0F172A"),
                FontAttributes = FontAttributes.Bold
            });

            index = searchIdx + query.Length;
        }

        return formatted;
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

    /// <summary>
    /// When non-null, the message content should use this FormattedString instead of <see cref="Content"/>
    /// to render highlighted search matches. Set when the message matches an active search query.
    /// </summary>
    public FormattedString? HighlightedContent { get; }

    /// <summary>Whether this message has highlighted search text (i.e., is a search result with matches).</summary>
    public bool IsHighlighted => HighlightedContent is not null;

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

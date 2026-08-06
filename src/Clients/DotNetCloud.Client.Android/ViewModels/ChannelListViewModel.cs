using System.Collections.ObjectModel;
using Android.Util;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Messages;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// ViewModel for the channel list screen.
/// Loads channels from the server and listens for real-time unread-count updates.
/// </summary>
public sealed partial class ChannelListViewModel : ObservableObject, IDisposable
{
    private readonly IChatRestClient _chatApi;
    private readonly IChatSignalRClient _signalR;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IChannelMuteStateService _muteState;
    private readonly ILogger<ChannelListViewModel> _logger;

    /// <summary>Raised when a channel is selected and the app should navigate to it.</summary>
    public event EventHandler<(Guid ChannelId, string Name)>? ChannelSelected;

    /// <summary>Raised when a channel's mute state changes (used to sync with ChannelDetailsViewModel).</summary>
    public event EventHandler<(Guid ChannelId, bool IsMuted)>? MuteStateChanged;

    /// <summary>Initializes a new <see cref="ChannelListViewModel"/>.</summary>
    public ChannelListViewModel(
        IChatRestClient chatApi,
        IChatSignalRClient signalR,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        IChannelMuteStateService muteState,
        ILogger<ChannelListViewModel> logger)
    {
        _chatApi = chatApi;
        _signalR = signalR;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _muteState = muteState;
        _logger = logger;

        _signalR.OnUnreadCountUpdated += OnUnreadCountUpdated;
        _signalR.OnNewChatMessage += OnNewMessage;
    }

    /// <summary>All visible channels, bound to the UI.</summary>
    public ObservableCollection<ChannelItemViewModel> Channels { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoadError))]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoadError))]
    private string? _errorMessage;

    /// <summary>True when a load attempt has finished and failed (not while still loading).</summary>
    public bool ShowInitialLoadError => !IsLoading && !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    private bool _hasCompletedInitialLoad;

    /// <summary>Whether the page is currently visible. Prevents background loads from setting ErrorMessage after the page disappears.</summary>
    internal bool IsActive { get; set; }

    /// <summary>Loads channels from the server.</summary>
    [RelayCommand]
    private async Task LoadChannelsAsync(CancellationToken ct)
    {
        Log.Info("DotNetCloud", "LoadChannelsAsync STARTED");
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // On cold start the first HTTP request may timeout while the connection pool
            // warms up. Retry silently so the error label never flashes before data arrives.
            var maxAttempts = HasCompletedInitialLoad ? 1 : 3;
            Log.Info("DotNetCloud", $"LoadChannelsAsync: maxAttempts={maxAttempts}");
            Exception? lastException = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (attempt > 1)
                        await Task.Delay(800, ct);

                    var (serverUrl, token) = await GetActiveCredentialsAsync(ct);
                    var channels = await FetchWithRetryAsync(
                        () => _chatApi.GetChannelsAsync(serverUrl, token, ct), ct);

                    Channels.Clear();
                    var muteStates = new Dictionary<Guid, bool>();
                    foreach (var ch in channels)
                    {
                        muteStates[ch.Id] = ch.IsMuted;
                        Channels.Add(new ChannelItemViewModel(ch.Id, ch.Name, ch.ChannelType, ch.UnreadCount, ch.HasMention, ch.IsMuted, ch.LastMessagePreview));
                    }

                    _muteState.ReplaceAll(muteStates);

                    await ResolveDmChannelNamesAsync(serverUrl, token, ct);

                    HasCompletedInitialLoad = true;
                    RecalculateTotalUnread();
                    return;
                }
                catch (Exception ex) when ((ex is TaskCanceledException or OperationCanceledException) && Channels.Count > 0)
                {
                    _logger.LogDebug(ex, "Transient timeout during channel reload; keeping existing data.");
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Chat channel load attempt {Attempt} of {MaxAttempts} failed: {Message}", attempt, maxAttempts, ex.Message);
                }
            }

            if (lastException is not null)
            {
                if (IsActive)
                {
                    var exceptionType = lastException.GetType().Name;
                    var statusCode = lastException is HttpRequestException hre ? hre.StatusCode?.ToString() ?? "null" : "N/A";
                    _logger.LogError(lastException, "Failed to load channels after {MaxAttempts} attempts. ExceptionType={ExceptionType}, StatusCode={StatusCode}.",
                        maxAttempts, exceptionType, statusCode);
                    ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(lastException);
                }
                else
                {
                    _logger.LogDebug(lastException, "Load failed while page inactive; suppressing error display.");
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Navigates into a channel when tapped.</summary>
    [RelayCommand]
    private void SelectChannel(ChannelItemViewModel item)
    {
        ChannelSelected?.Invoke(this, (item.ChannelId, item.Name));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _signalR.OnUnreadCountUpdated -= OnUnreadCountUpdated;
        _signalR.OnNewChatMessage -= OnNewMessage;
    }

    // ── Direct Message ──────────────────────────────────────────────

    /// <summary>Raised when a DM is created and the app should navigate to it.</summary>
    public event EventHandler<(Guid ChannelId, string Name)>? DmCreated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDmPickerVisible))]
    private bool _isDmPickerOpen;

    [ObservableProperty]
    private string _dmSearchQuery = string.Empty;

    [ObservableProperty]
    private bool _isDmSearching;

    [ObservableProperty]
    private string? _dmSearchError;

    /// <summary>User search results for the DM picker.</summary>
    public ObservableCollection<UserSearchResult> DmSearchResults { get; } = [];

    /// <summary>Whether the DM user picker is visible.</summary>
    public bool IsDmPickerVisible => IsDmPickerOpen;

    /// <summary>Opens the DM user picker.</summary>
    [RelayCommand]
    private void OpenDmPicker()
    {
        IsDmPickerOpen = true;
        DmSearchQuery = string.Empty;
        DmSearchResults.Clear();
        DmSearchError = null;
    }

    /// <summary>Closes the DM user picker.</summary>
    [RelayCommand]
    private void CloseDmPicker()
    {
        IsDmPickerOpen = false;
        DmSearchQuery = string.Empty;
        DmSearchResults.Clear();
        DmSearchError = null;
    }

    /// <summary>Searches users for DM creation with debounce via the UI binding.</summary>
    [RelayCommand]
    private async Task SearchDmUsersAsync(string query, CancellationToken ct)
    {
        DmSearchQuery = query;

        if (string.IsNullOrWhiteSpace(query))
        {
            DmSearchResults.Clear();
            DmSearchError = null;
            return;
        }

        IsDmSearching = true;
        DmSearchError = null;

        try
        {
            var (serverUrl, token) = await GetActiveCredentialsAsync(ct);
            var results = await _chatApi.SearchUsersAsync(serverUrl, token, query, maxResults: 20, ct);

            DmSearchResults.Clear();
            foreach (var user in results)
                DmSearchResults.Add(user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DM user search failed for query '{Query}'.", query);
            DmSearchError = "Search failed. Please try again.";
            DmSearchResults.Clear();
        }
        finally
        {
            IsDmSearching = false;
        }
    }

    /// <summary>Creates or opens a DM channel with the selected user and navigates to it.</summary>
    [RelayCommand]
    private async Task StartDmAsync(UserSearchResult user, CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetActiveCredentialsAsync(ct);
            var channel = await _chatApi.GetOrCreateDmAsync(serverUrl, token, user.UserId, ct);

            IsDmPickerOpen = false;
            DmSearchQuery = string.Empty;
            DmSearchResults.Clear();

            // Use the target user's display name as the channel name
            var displayName = user.DisplayName ?? user.UserId.ToString()[..8];
            DmCreated?.Invoke(this, (channel.Id, displayName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create DM with user {UserId}.", user.UserId);
            DmSearchError = "Failed to start conversation. Please try again.";
        }
    }

    // ── Mute toggle ──────────────────────────────────────────────────

    /// <summary>Toggles the mute state for a channel.</summary>
    [RelayCommand]
    private async Task ToggleMuteAsync(ChannelItemViewModel item, CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetActiveCredentialsAsync(ct);
            if (item.IsMuted)
                await _chatApi.UnmuteChannelAsync(serverUrl, token, item.ChannelId, ct);
            else
                await _chatApi.MuteChannelAsync(serverUrl, token, item.ChannelId, ct);

            item.IsMuted = !item.IsMuted;
            _muteState.SetMuted(item.ChannelId, item.IsMuted);

            // Notify mute state change for ChannelDetailsViewModel sync
            MuteStateChanged?.Invoke(this, (item.ChannelId, item.IsMuted));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle mute for channel {ChannelId}.", item.ChannelId);
        }
    }

    // ── Real-time handlers ───────────────────────────────────────────

    private void OnUnreadCountUpdated(object? sender, ChatUnreadCountUpdatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // GUID string casing can differ between the server payload and Guid.ToString(),
            // so match case-insensitively to avoid missing the target channel.
            var item = Channels.FirstOrDefault(c =>
                string.Equals(c.ChannelId.ToString(), e.ChannelId, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
            {
                item.UnreadCount = e.UnreadCount;
                item.HasMention = e.HasMention;
            }

            RecalculateTotalUnread();
        });
    }

    /// <summary>Recomputes the sum of unread counts and broadcasts it for the tab indicator.</summary>
    private void RecalculateTotalUnread()
    {
        var total = Channels.Sum(c => c.UnreadCount);
        WeakReferenceMessenger.Default.Send(new TotalUnreadCountChangedMessage(total));
    }

    /// <summary>Resolves DM channel names to show the other participant's display name.</summary>
    private async Task ResolveDmChannelNamesAsync(string serverUrl, string token, CancellationToken ct)
    {
        var dmChannels = Channels.Where(c => c.ChannelType == "DirectMessage").ToList();
        if (dmChannels.Count == 0)
            return;

        try
        {
            // Get current user ID from the access token
            var currentUserId = AccessTokenUserIdExtractor.ExtractUserId(token);
            if (currentUserId == Guid.Empty)
                return;

            // Parse DM channel names (format: DM-{userId1}-{userId2}) to find other user IDs
            var otherUserIds = new List<Guid>();
            var channelToOtherUser = new Dictionary<Guid, Guid>(); // channelId → otherUserId

            foreach (var dm in dmChannels)
            {
                var parts = dm.Name.Split('-');
                if (parts.Length >= 3
                    && Guid.TryParse(parts[1], out var guid1)
                    && Guid.TryParse(parts[2], out var guid2))
                {
                    var other = guid1 == currentUserId ? guid2 : guid1;
                    channelToOtherUser[dm.ChannelId] = other;
                    otherUserIds.Add(other);
                }
            }

            if (otherUserIds.Count == 0)
                return;

            // Resolve display names from server
            var names = await _chatApi.ResolveDisplayNamesAsync(serverUrl, token, otherUserIds.Distinct().ToList(), ct);

            // Update channel names
            foreach (var dm in dmChannels)
            {
                if (channelToOtherUser.TryGetValue(dm.ChannelId, out var otherUserId)
                    && names.TryGetValue(otherUserId, out var displayName))
                {
                    dm.Name = displayName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve DM channel display names.");
        }
    }

    private void OnNewMessage(object? sender, ChatMessageReceivedEventArgs e) { /* handled via unread update */ }

    private async Task<(string serverUrl, string token)> GetActiveCredentialsAsync(CancellationToken ct)
    {
        Log.Info("DotNetCloud", "GetActiveCredentialsAsync: STARTED");
        var connection = _serverStore.GetActive()
                         ?? throw new InvalidOperationException("No active server connection.");
        var sv = connection.ServerBaseUrl;
        Log.Info("DotNetCloud", $"GetActiveCredentials: server={sv}");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct);
        Log.Info("DotNetCloud", $"GetActiveCredentials: token={(token is not null ? "present" : "null")}, length={token?.Length ?? 0}");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("No access token found. Please log in again.");
        return (connection.ServerBaseUrl, token);
    }

    private static async Task<T> FetchWithRetryAsync<T>(Func<Task<T>> fetchFunc, CancellationToken ct)
    {
        try
        {
            return await fetchFunc();
        }
        catch (Exception ex) when ((ex is TaskCanceledException or OperationCanceledException) && !ct.IsCancellationRequested)
        {
            // Single silent retry for transient timeout (not explicit cancellation)
            await Task.Delay(500, ct);
            return await fetchFunc();
        }
    }
}

/// <summary>Represents a single channel row in the channel list.</summary>
public sealed partial class ChannelItemViewModel : ObservableObject
{
    /// <summary>Initializes a channel list item.</summary>
    public ChannelItemViewModel(Guid channelId, string name, string? channelType, int unreadCount, bool hasMention, bool isMuted, string? lastMessagePreview)
    {
        ChannelId = channelId;
        Name = name;
        ChannelType = channelType;
        UnreadCount = unreadCount;
        HasMention = hasMention;
        IsMuted = isMuted;
        LastMessagePreview = lastMessagePreview;
    }

    /// <summary>Channel identifier.</summary>
    public Guid ChannelId { get; }

    /// <summary>Display name of the channel.</summary>
    public string Name { get; set; }

    /// <summary>Channel type: Public, Private, DirectMessage, or Group.</summary>
    public string? ChannelType { get; }

    /// <summary>Unread message count (updated in real-time).</summary>
    [ObservableProperty] private int _unreadCount;

    /// <summary>Whether any unread messages contain a mention.</summary>
    [ObservableProperty] private bool _hasMention;

    /// <summary>Whether notifications for this channel are muted.</summary>
    [ObservableProperty] private bool _isMuted;

    /// <summary>Preview of the last message.</summary>
    public string? LastMessagePreview { get; }
}

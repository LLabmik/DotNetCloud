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
    private readonly ICoreHubClient _signalR;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IChannelMuteStateService _muteState;
    private readonly ILogger<ChannelListViewModel> _logger;

    /// <summary>
    /// Maps each DirectMessage channel ID to the other participant's user ID.
    /// Populated while resolving DM names; drives the presence-dot seed and live updates.
    /// </summary>
    private readonly Dictionary<Guid, Guid> _dmChannelToOtherUser = new();

    /// <summary>Raised when a channel is selected and the app should navigate to it.</summary>
    public event EventHandler<(Guid ChannelId, string Name)>? ChannelSelected;

    /// <summary>Raised when a channel's mute state changes (used to sync with ChannelDetailsViewModel).</summary>
    public event EventHandler<(Guid ChannelId, bool IsMuted)>? MuteStateChanged;

    /// <summary>Initializes a new <see cref="ChannelListViewModel"/>.</summary>
    public ChannelListViewModel(
        IChatRestClient chatApi,
        ICoreHubClient signalR,
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
        _signalR.OnUserPresenceChanged += OnUserPresenceChanged;
        _signalR.Reconnected += OnReconnected;
    }

    /// <summary>All visible channels (flat, source of truth for real-time updates).</summary>
    public ObservableCollection<ChannelItemViewModel> Channels { get; } = [];

    /// <summary>
    /// Channels grouped into display sections for the UI — "Channels" (Public/Private) and
    /// "Direct Messages" (DirectMessage/Group) — mirroring the Blazor chat sidebar.
    /// </summary>
    public ObservableCollection<ChannelGroupViewModel> ChannelGroups { get; } = [];

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
                        Channels.Add(new ChannelItemViewModel(ch.Id, ch.Name, ch.ChannelType, ch.UnreadCount, ch.HasMention, ch.IsMuted, ch.LastMessagePreview)
                        {
                            // DM rows carry the peer ID the server resolved (used for presence dots).
                            OtherUserId = ch.OtherUserId is { } peer && peer != Guid.Empty ? peer : null
                        });
                    }

                    _muteState.ReplaceAll(muteStates);

                    RebuildChannelGroups();

                    await ResolveDmChannelNamesAsync(serverUrl, token, ct);

                    // Seed DM presence dots from a snapshot (no need to wait for a live
                    // presence event). Fire-and-forget: the list shows immediately and dots
                    // populate when the query returns; failures leave dots offline.
                    _ = RefreshPresenceAsync();

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

    /// <summary>
    /// Rebuilds <see cref="ChannelGroups"/> from the flat <see cref="Channels"/> list.
    /// Mirrors the Blazor chat sidebar: Public/Private channels go under "Channels", and
    /// DirectMessage/Group channels under "Direct Messages". Empty sections are omitted.
    /// </summary>
    private void RebuildChannelGroups()
    {
        ChannelGroups.Clear();
        if (Channels.Count == 0)
            return;

        var channelsSection = Channels.Where(c => c.ChannelType is not ("DirectMessage" or "Group")).ToList();
        var dmSection = Channels.Where(c => c.ChannelType is "DirectMessage" or "Group").ToList();

        if (channelsSection.Count > 0)
            ChannelGroups.Add(new ChannelGroupViewModel("Channels", channelsSection));
        if (dmSection.Count > 0)
            ChannelGroups.Add(new ChannelGroupViewModel("Direct Messages", dmSection));
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
        _signalR.OnUserPresenceChanged -= OnUserPresenceChanged;
        _signalR.Reconnected -= OnReconnected;
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

    // ── DM presence dots ─────────────────────────────────────────────

    /// <summary>
    /// Queries a current presence snapshot for all DM peers and updates the matching dots.
    /// Safe to call from any thread; runs when the DM peer map is non-empty.
    /// </summary>
    private async Task RefreshPresenceAsync()
    {
        if (_dmChannelToOtherUser.Count == 0)
            return;

        try
        {
            var peerIds = _dmChannelToOtherUser.Values.Distinct().ToList();
            var statuses = await _signalR.GetPresenceStatusAsync(peerIds);
            if (statuses.Count == 0)
                return;

            Action dispatch = () =>
            {
                foreach (var item in Channels)
                {
                    if (item.ChannelType == "DirectMessage"
                        && item.OtherUserId is { } peer
                        && statuses.TryGetValue(peer, out var status))
                    {
                        item.PresenceStatus = status;
                    }
                }
            };

            try
            {
                MainThread.BeginInvokeOnMainThread(dispatch);
            }
            catch
            {
                dispatch(); // unit-test environment without a UI thread
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh DM presence dots; leaving dots offline.");
        }
    }

    /// <summary>Re-seeds DM presence after the hub reconnects (peers may have changed presence while disconnected).</summary>
    private void OnReconnected(object? sender, EventArgs e)
    {
        Log.Info("DotNetCloud", "ChannelListViewModel: hub reconnected — re-querying DM presence.");
        _ = Task.Run(async () => await RefreshPresenceAsync());
    }

    private void OnUserPresenceChanged(object? sender, UserPresenceChangedEventArgs e)
    {
        // Find the DM channel whose peer's presence changed and update the shared item in
        // place (the flat Channels list and grouped ChannelGroups share item instances).
        Action dispatch = () =>
        {
            foreach (var item in Channels)
            {
                if (item.ChannelType == "DirectMessage" && item.OtherUserId == e.UserId)
                {
                    item.PresenceStatus = e.Status;
                    return;
                }
            }
        };

        try
        {
            MainThread.BeginInvokeOnMainThread(dispatch);
        }
        catch
        {
            dispatch(); // unit-test environment without a UI thread
        }
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
        Log.Info("DotNetCloud", $"ResolveDmChannelNamesAsync: totalChannels={Channels.Count}, dmChannels={dmChannels.Count}");

        if (dmChannels.Count == 0)
        {
            Log.Info("DotNetCloud", "ResolveDmChannelNamesAsync: no DM channels found, skipping resolution.");
            return;
        }

        try
        {
            // Extract current user ID from the id_token (signed JWT, not encrypted JWE).
            // The access token is JWE-encrypted and cannot be decoded client-side.
            var currentUserId = await GetCurrentUserIdAsync(serverUrl, ct);

            // Build the DM-channel → peer map used for presence dots + name resolution.
            // Prefer the peer ID the server sends (ChannelDto.OtherUserId, already set on the
            // item in LoadChannelsAsync); fall back to parsing the legacy raw DM name format
            // (DM-{userId1}-{userId2}) for servers that return unresolved channel names.
            _dmChannelToOtherUser.Clear();
            var otherUserIds = new List<Guid>();

            foreach (var dm in dmChannels)
            {
                var peerId = dm.OtherUserId is { } p && p != Guid.Empty
                    ? p
                    : ParseDmChannelPeer(dm.Name, currentUserId);

                if (peerId != Guid.Empty)
                {
                    _dmChannelToOtherUser[dm.ChannelId] = peerId;
                    dm.OtherUserId = peerId;
                    otherUserIds.Add(peerId);
                    Log.Info("DotNetCloud", $"ResolveDmChannelNamesAsync: DM channel {dm.ChannelId} → other user={peerId}");
                }
                else
                {
                    Log.Warn("DotNetCloud", $"ResolveDmChannelNamesAsync: no peer user ID for DM channel '{dm.Name}'");
                }
            }

            if (otherUserIds.Count == 0)
            {
                Log.Warn("DotNetCloud", "ResolveDmChannelNamesAsync: no other user IDs extracted.");
                return;
            }

            Log.Info("DotNetCloud", $"ResolveDmChannelNamesAsync: calling ResolveDisplayNamesAsync for {otherUserIds.Count} userIds");
            var names = await _chatApi.ResolveDisplayNamesAsync(serverUrl, token, otherUserIds.Distinct().ToList(), ct);
            Log.Info("DotNetCloud", $"ResolveDmChannelNamesAsync: resolved {names.Count} display names");

            foreach (var dm in dmChannels)
            {
                if (_dmChannelToOtherUser.TryGetValue(dm.ChannelId, out var otherUserId)
                    && names.TryGetValue(otherUserId, out var displayName))
                {
                    Log.Info("DotNetCloud", $"ResolveDmChannelNamesAsync: updating DM name '{dm.Name}' → '{displayName}'");
                    dm.Name = displayName;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"ResolveDmChannelNamesAsync FAILED: {ex.GetType().Name}: {ex.Message}");
            _logger.LogWarning(ex, "Failed to resolve DM channel display names.");
        }
    }

    /// <summary>
    /// Parses the legacy raw DM channel name format <c>DM-{userId1}-{userId2}</c> to find the other
    /// participant. Returns <see cref="Guid.Empty"/> when the name is not in that format (e.g. it is
    /// already a resolved display name — in that case the peer comes from the server's
    /// <c>OtherUserId</c>, which <see cref="LoadChannelsAsync"/> stores on the item).
    /// </summary>
    private static Guid ParseDmChannelPeer(string dmName, Guid currentUserId)
    {
        var parts = dmName.Split('-');
        // DM name format: DM-{guid1}-{guid2}
        // Each GUID has 5 dash-segments, so total = 1 (DM) + 5 + 5 = 11 parts
        if (parts.Length == 11
            && Guid.TryParse(string.Join("-", parts[1..6]), out var guid1)
            && Guid.TryParse(string.Join("-", parts[6..11]), out var guid2))
        {
            return guid1 == currentUserId ? guid2 : guid1;
        }

        return Guid.Empty;
    }

    /// <summary>Gets the current user's ID from the id_token's <c>sub</c> claim.</summary>
    private async Task<Guid> GetCurrentUserIdAsync(string serverUrl, CancellationToken ct)
    {
        try
        {
            var idToken = await _tokenStore.GetIdTokenAsync(serverUrl, ct);
            if (!string.IsNullOrWhiteSpace(idToken))
            {
                var userId = AccessTokenUserIdExtractor.ExtractUserId(idToken);
                Log.Info("DotNetCloud", $"GetCurrentUserIdAsync: extracted from id_token: {userId}");
                return userId;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"GetCurrentUserIdAsync: failed to extract from id_token: {ex.Message}");
        }

        Log.Warn("DotNetCloud", "GetCurrentUserIdAsync: no id_token available, cannot resolve DM names.");
        return Guid.Empty;
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
        _name = name;
        ChannelType = channelType;
        UnreadCount = unreadCount;
        HasMention = hasMention;
        IsMuted = isMuted;
        LastMessagePreview = lastMessagePreview;
    }

    /// <summary>Channel identifier.</summary>
    public Guid ChannelId { get; }

    /// <summary>Display name of the channel.</summary>
    [ObservableProperty] private string _name;

    /// <summary>Channel type: Public, Private, DirectMessage, or Group.</summary>
    public string? ChannelType { get; }

    /// <summary>True for DirectMessage rows (drives the presence-dot visibility in XAML).</summary>
    public bool IsDirectMessage => ChannelType == "DirectMessage";

    /// <summary>
    /// For DirectMessage rows: the other participant's user ID (used to match live
    /// presence events and the initial snapshot). Null for other channel types.
    /// </summary>
    public Guid? OtherUserId { get; set; }

    /// <summary>
    /// The DM peer's 4-state presence (DirectMessage rows only): "Online", "Away",
    /// "DoNotDisturb", or "Offline". Defaults to offline.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnline))]
    private string _presenceStatus = "Offline";

    /// <summary>True when the DM peer has any non-offline presence (any active connection).</summary>
    public bool IsOnline => PresenceStatus != "Offline";

    /// <summary>Unread message count (updated in real-time).</summary>
    [ObservableProperty] private int _unreadCount;

    /// <summary>Whether any unread messages contain a mention.</summary>
    [ObservableProperty] private bool _hasMention;

    /// <summary>Whether notifications for this channel are muted.</summary>
    [ObservableProperty] private bool _isMuted;

    /// <summary>Preview of the last message.</summary>
    public string? LastMessagePreview { get; }
}

/// <summary>
/// A section of the channel list ("Channels" or "Direct Messages"). Inherits from
/// <see cref="List{T}"/> so the grouped <see cref="Microsoft.Maui.Controls.CollectionView"/>
/// can enumerate its items, while <see cref="Title"/> is bound by the group header template.
/// </summary>
public sealed class ChannelGroupViewModel : List<ChannelItemViewModel>
{
    /// <summary>Initializes a new channel-list group.</summary>
    public ChannelGroupViewModel(string title, IEnumerable<ChannelItemViewModel> items)
        : base(items)
    {
        Title = title;
    }

    /// <summary>Header text shown above the group.</summary>
    public string Title { get; }
}

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
                        Channels.Add(new ChannelItemViewModel(ch.Id, ch.Name, ch.UnreadCount, ch.HasMention, ch.IsMuted, ch.LastMessagePreview));
                    }

                    _muteState.ReplaceAll(muteStates);

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
    public ChannelItemViewModel(Guid channelId, string name, int unreadCount, bool hasMention, bool isMuted, string? lastMessagePreview)
    {
        ChannelId = channelId;
        Name = name;
        UnreadCount = unreadCount;
        HasMention = hasMention;
        IsMuted = isMuted;
        LastMessagePreview = lastMessagePreview;
    }

    /// <summary>Channel identifier.</summary>
    public Guid ChannelId { get; }

    /// <summary>Display name of the channel.</summary>
    public string Name { get; }

    /// <summary>Unread message count (updated in real-time).</summary>
    [ObservableProperty] private int _unreadCount;

    /// <summary>Whether any unread messages contain a mention.</summary>
    [ObservableProperty] private bool _hasMention;

    /// <summary>Whether notifications for this channel are muted.</summary>
    [ObservableProperty] private bool _isMuted;

    /// <summary>Preview of the last message.</summary>
    public string? LastMessagePreview { get; }
}

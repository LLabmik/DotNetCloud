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
/// ViewModel for the channel list screen.
/// Loads channels from the server and listens for real-time unread-count updates.
/// </summary>
public sealed partial class ChannelListViewModel : ObservableObject, IDisposable
{
    private readonly IChatRestClient _chatApi;
    private readonly IChatSignalRClient _signalR;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<ChannelListViewModel> _logger;

    /// <summary>Raised when a channel is selected and the app should navigate to it.</summary>
    public event EventHandler<(Guid ChannelId, string Name)>? ChannelSelected;

    /// <summary>Initializes a new <see cref="ChannelListViewModel"/>.</summary>
    public ChannelListViewModel(
        IChatRestClient chatApi,
        IChatSignalRClient signalR,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<ChannelListViewModel> logger)
    {
        _chatApi = chatApi;
        _signalR = signalR;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
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
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // On cold start the first HTTP request may timeout while the connection pool
            // warms up. Retry silently so the error label never flashes before data arrives.
            var maxAttempts = HasCompletedInitialLoad ? 1 : 3;
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
                    foreach (var ch in channels)
                        Channels.Add(new ChannelItemViewModel(ch.Id, ch.Name, ch.UnreadCount, ch.HasMention, ch.LastMessagePreview));

                    HasCompletedInitialLoad = true;
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
                    if (attempt < maxAttempts)
                        _logger.LogDebug(ex, "Initial load attempt {Attempt} of {MaxAttempts} failed; retrying.", attempt, maxAttempts);
                }
            }

            if (lastException is not null)
            {
                if (IsActive)
                {
                    _logger.LogError(lastException, "Failed to load channels.");
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

    // ── Real-time handlers ───────────────────────────────────────────

    private void OnUnreadCountUpdated(object? sender, ChatUnreadCountUpdatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var item = Channels.FirstOrDefault(c => c.ChannelId.ToString() == e.ChannelId);
            if (item is not null)
            {
                item.UnreadCount = e.UnreadCount;
                item.HasMention = e.HasMention;
            }
        });
    }

    private void OnNewMessage(object? sender, ChatMessageReceivedEventArgs e) { /* handled via unread update */ }

    private async Task<(string serverUrl, string token)> GetActiveCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
                         ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct);
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
    public ChannelItemViewModel(Guid channelId, string name, int unreadCount, bool hasMention, string? lastMessagePreview)
    {
        ChannelId = channelId;
        Name = name;
        UnreadCount = unreadCount;
        HasMention = hasMention;
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

    /// <summary>Preview of the last message.</summary>
    public string? LastMessagePreview { get; }
}

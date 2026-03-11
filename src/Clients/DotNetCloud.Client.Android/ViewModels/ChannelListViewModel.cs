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
    public event EventHandler<Guid>? ChannelSelected;

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
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Loads channels from the server.</summary>
    [RelayCommand]
    private async Task LoadChannelsAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        Channels.Clear();

        try
        {
            var (serverUrl, token) = await GetActiveCredentialsAsync(ct).ConfigureAwait(false);
            var channels = await _chatApi.GetChannelsAsync(serverUrl, token, ct).ConfigureAwait(false);

            foreach (var ch in channels)
                Channels.Add(new ChannelItemViewModel(ch.Id, ch.Name, ch.UnreadCount, ch.HasMention, ch.LastMessagePreview));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load channels.");
            ErrorMessage = "Failed to load channels. Pull to refresh.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Navigates into a channel when tapped.</summary>
    [RelayCommand]
    private void SelectChannel(Guid channelId)
    {
        ChannelSelected?.Invoke(this, channelId);
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
        var item = Channels.FirstOrDefault(c => c.ChannelId.ToString() == e.ChannelId);
        if (item is not null)
        {
            item.UnreadCount = e.UnreadCount;
            item.HasMention = e.HasMention;
        }
    }

    private void OnNewMessage(object? sender, ChatMessageReceivedEventArgs e) { /* handled via unread update */ }

    private async Task<(string serverUrl, string token)> GetActiveCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
                         ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("No access token found.");
        return (connection.ServerBaseUrl, token);
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

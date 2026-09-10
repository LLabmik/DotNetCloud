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
/// ViewModel for the channel details page.
/// Displays channel info, members (with live 4-state presence dots), mute/notification
/// preference, and leave-channel action.
/// </summary>
public sealed partial class ChannelDetailsViewModel : ObservableObject, IDisposable
{
    private readonly IChatRestClient _chatApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ICoreHubClient _signalR;
    private readonly ILogger<ChannelDetailsViewModel> _logger;

    private Guid _channelId;
    private string? _serverUrl;
    private string? _accessToken;

    /// <summary>Raised when the user successfully leaves the channel.</summary>
    public event EventHandler? ChannelLeft;

    /// <summary>Initializes a new <see cref="ChannelDetailsViewModel"/>.</summary>
    public ChannelDetailsViewModel(
        IChatRestClient chatApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ICoreHubClient signalR,
        ILogger<ChannelDetailsViewModel> logger)
    {
        _chatApi = chatApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _signalR = signalR;
        _logger = logger;

        _signalR.OnUserPresenceChanged += OnUserPresenceChanged;
        _signalR.Reconnected += OnReconnected;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _signalR.OnUserPresenceChanged -= OnUserPresenceChanged;
        _signalR.Reconnected -= OnReconnected;
    }

    /// <summary>Channel display name.</summary>
    [ObservableProperty]
    private string _channelName = string.Empty;

    /// <summary>Channel topic/description.</summary>
    [ObservableProperty]
    private string? _channelTopic;

    /// <summary>Whether push notifications for this channel are muted.</summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>The current channel ID (exposed for cross-VM sync).</summary>
    public Guid ChannelId => _channelId;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>All channel members.</summary>
    public ObservableCollection<ChannelMemberItemViewModel> Members { get; } = [];

    /// <summary>Header text for the members section showing count.</summary>
    public string MembersHeaderText => $"Members ({Members.Count})";

    /// <summary>Formatted member count for channel info block.</summary>
    public string MemberCountDisplay => Members.Count == 1 ? "1 member" : $"{Members.Count} members";

    /// <summary>Prepares the view model for the given channel.</summary>
    public void Prepare(Guid channelId, string channelName, string? channelTopic = null)
    {
        _channelId = channelId;
        ChannelName = channelName;
        ChannelTopic = channelTopic;
    }

    /// <summary>Loads channel details and member list from the server.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var connection = _serverStore.GetActive()
                             ?? throw new InvalidOperationException("No active server connection.");
            _serverUrl = connection.ServerBaseUrl;
            _accessToken = await _tokenStore.GetAccessTokenAsync(_serverUrl, ct)
                           ?? throw new InvalidOperationException("No access token found.");

            var members = await _chatApi.GetChannelMembersAsync(_serverUrl, _accessToken, _channelId, ct);

            Members.Clear();
            // Member presence starts gray/offline; the CoreHub snapshot (RefreshPresenceAsync)
            // below seeds the live 4-state dot — the REST DTO's stale IsOnline field is ignored.
            foreach (var m in members.OrderBy(m => m.Role).ThenBy(m => m.DisplayName))
                Members.Add(new ChannelMemberItemViewModel(m.UserId, m.DisplayName, m.Role, "Offline"));

            OnPropertyChanged(nameof(MembersHeaderText));
            OnPropertyChanged(nameof(MemberCountDisplay));

            _ = RefreshPresenceAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load channel details for {ChannelId}.", _channelId);
            ErrorMessage = "Failed to load channel details.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Queries a current presence snapshot for the member list and updates each member's
    /// presence dot (live 4-state). Failures leave members gray.
    /// </summary>
    private async Task RefreshPresenceAsync()
    {
        if (Members.Count == 0)
            return;

        try
        {
            var memberIds = Members.Select(m => m.UserId).Distinct().ToList();
            var statuses = await _signalR.GetPresenceStatusAsync(memberIds);
            if (statuses.Count == 0)
                return;

            Action dispatch = () =>
            {
                foreach (var member in Members)
                {
                    if (statuses.TryGetValue(member.UserId, out var status))
                        member.PresenceStatus = status;
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
            _logger.LogWarning(ex, "Failed to refresh member presence for channel {ChannelId}; members stay offline.", _channelId);
        }
    }

    /// <summary>Re-seeds member presence after the hub reconnects.</summary>
    private void OnReconnected(object? sender, EventArgs e)
    {
        _ = Task.Run(async () => await RefreshPresenceAsync());
    }

    /// <summary>
    /// Updates a member's presence dot in place when a live presence event arrives.
    /// </summary>
    private void OnUserPresenceChanged(object? sender, UserPresenceChangedEventArgs e)
    {
        Action dispatch = () =>
        {
            foreach (var member in Members)
            {
                if (member.UserId == e.UserId)
                {
                    member.PresenceStatus = e.Status;
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

    /// <summary>Leaves the current channel.</summary>
    [RelayCommand]
    private async Task LeaveChannelAsync(CancellationToken ct)
    {
        if (_serverUrl is null || _accessToken is null)
            return;

        try
        {
            await _chatApi.LeaveChannelAsync(_serverUrl, _accessToken, _channelId, ct);
            await MainThread.InvokeOnMainThreadAsync(() => ChannelLeft?.Invoke(this, EventArgs.Empty));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave channel {ChannelId}.", _channelId);
            ErrorMessage = "Failed to leave channel.";
        }
    }

    /// <summary>Called when <see cref="IsMuted"/> changes. Calls the server API.</summary>
    partial void OnIsMutedChanged(bool value)
    {
        if (_serverUrl is null || _accessToken is null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                if (value)
                    await _chatApi.MuteChannelAsync(_serverUrl, _accessToken, _channelId);
                else
                    await _chatApi.UnmuteChannelAsync(_serverUrl, _accessToken, _channelId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to toggle mute for channel {ChannelId} from details page.", _channelId);
                // Revert the toggle on failure
                await MainThread.InvokeOnMainThreadAsync(() => IsMuted = !value);
            }
        });
    }
}

/// <summary>Represents a single member row in the channel details member list.</summary>
public sealed partial class ChannelMemberItemViewModel : ObservableObject
{
    /// <summary>Initializes a channel member item.</summary>
    public ChannelMemberItemViewModel(Guid userId, string displayName, string role, string presenceStatus = "Offline")
    {
        UserId = userId;
        DisplayName = displayName;
        Role = role;
        _presenceStatus = presenceStatus;
        Initials = GetInitials(displayName);
        RoleLabel = role switch
        {
            "Owner" => "Owner",
            "Admin" => "Admin",
            _ => string.Empty
        };
    }

    /// <summary>User identifier.</summary>
    public Guid UserId { get; }

    /// <summary>Display name of the member.</summary>
    public string DisplayName { get; }

    /// <summary>Member role string (Owner, Admin, Member).</summary>
    public string Role { get; }

    /// <summary>
    /// The member's 4-state presence: "Online", "Away", "DoNotDisturb", or "Offline".
    /// Seeded from the CoreHub snapshot and kept live via presence events.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnline))]
    private string _presenceStatus;

    /// <summary>True when the member has any non-offline presence (any active connection).</summary>
    public bool IsOnline => PresenceStatus != "Offline";

    /// <summary>One or two letter initials for the avatar placeholder.</summary>
    public string Initials { get; }

    /// <summary>Formatted role label (empty for regular members).</summary>
    public string RoleLabel { get; }

    private static string GetInitials(string displayName)
    {
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : displayName.Length > 0
                ? displayName[0].ToString().ToUpperInvariant()
                : "?";
    }
}

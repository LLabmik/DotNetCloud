using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// ViewModel for the channel details page.
/// Displays channel info, members, mute/notification preference, and leave-channel action.
/// </summary>
public sealed partial class ChannelDetailsViewModel : ObservableObject
{
    private readonly IChatRestClient _chatApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
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
        ILogger<ChannelDetailsViewModel> logger)
    {
        _chatApi = chatApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
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
            foreach (var m in members.OrderBy(m => m.Role).ThenBy(m => m.DisplayName))
                Members.Add(new ChannelMemberItemViewModel(m.UserId, m.DisplayName, m.Role, m.IsOnline));

            OnPropertyChanged(nameof(MembersHeaderText));
            OnPropertyChanged(nameof(MemberCountDisplay));
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

    /// <summary>Leaves the current channel.</summary>
    [RelayCommand]
    private async Task LeaveChannelAsync(CancellationToken ct)
    {
        if (_serverUrl is null || _accessToken is null) return;

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
}

/// <summary>Represents a single member row in the channel details member list.</summary>
public sealed class ChannelMemberItemViewModel
{
    /// <summary>Initializes a channel member item.</summary>
    public ChannelMemberItemViewModel(Guid userId, string displayName, string role, bool isOnline)
    {
        UserId = userId;
        DisplayName = displayName;
        Role = role;
        IsOnline = isOnline;
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

    /// <summary>Whether the member is currently online.</summary>
    public bool IsOnline { get; }

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

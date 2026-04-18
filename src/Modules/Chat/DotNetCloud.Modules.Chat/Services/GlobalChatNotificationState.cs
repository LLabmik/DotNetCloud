using DotNetCloud.Core.Capabilities;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Scoped state service that subscribes to chat notification events from
/// <see cref="IChatMessageNotifier"/> and exposes incoming call / message state
/// for the top-level layout to render notifications regardless of the active module.
/// </summary>
public sealed class GlobalChatNotificationState : IDisposable
{
    private readonly IChatMessageNotifier _notifier;
    private readonly IUserDirectory _userDirectory;
    private System.Timers.Timer? _ringTimer;
    private System.Timers.Timer? _toastTimer;
    private Guid _currentUserId;
    private Guid? _activeCallId;

    /// <summary>Whether an incoming call notification should be displayed.</summary>
    public bool ShowIncomingCall { get; private set; }

    /// <summary>The call ID of the incoming call.</summary>
    public Guid? IncomingCallId { get; private set; }

    /// <summary>The channel ID where the call originates.</summary>
    public Guid? IncomingCallChannelId { get; private set; }

    /// <summary>The user ID of the caller or inviter.</summary>
    public Guid? IncomingCallInitiatorId { get; private set; }

    /// <summary>Display name of the caller.</summary>
    public string CallerName { get; private set; } = string.Empty;

    /// <summary>Avatar URL of the caller, if available.</summary>
    public string? CallerAvatarUrl { get; private set; }

    /// <summary>Name of the channel the call is from.</summary>
    public string ChannelName { get; private set; } = string.Empty;

    /// <summary>Media type of the incoming call (Audio or Video).</summary>
    public string MediaType { get; private set; } = "Audio";

    /// <summary>Whether this is a mid-call invite rather than a fresh call.</summary>
    public bool IsMidCallInvite { get; private set; }

    /// <summary>Number of participants currently in the call (for mid-call invites).</summary>
    public int ParticipantCount { get; private set; }

    /// <summary>Remaining seconds before the ring timeout auto-dismisses.</summary>
    public int RemainingSeconds { get; private set; }

    /// <summary>Whether the ringing animation should play.</summary>
    public bool IsRinging { get; private set; }

    /// <summary>Pending call accept that <c>ChatPageLayout</c> should consume.</summary>
    public PendingCallAccept? PendingAccept { get; private set; }

    /// <summary>Whether a message toast notification should be displayed.</summary>
    public bool ShowMessageToast { get; private set; }

    /// <summary>The channel ID the toast message is from.</summary>
    public Guid? ToastChannelId { get; private set; }

    /// <summary>The channel name the toast message is from.</summary>
    public string ToastChannelName { get; private set; } = string.Empty;

    /// <summary>The sender's display name for the toast.</summary>
    public string ToastSenderName { get; private set; } = string.Empty;

    /// <summary>Message preview text for the toast.</summary>
    public string ToastMessagePreview { get; private set; } = string.Empty;

    /// <summary>Raised when any notification state property changes.</summary>
    public event Action? OnChange;

    /// <summary>Raised when a call is accepted from the global notification overlay.
    /// ChatPageLayout subscribes to handle WebRTC join.</summary>
    public event Action? OnCallAccepted;

    /// <summary>
    /// Creates a new instance backed by the given notifier and user directory.
    /// </summary>
    public GlobalChatNotificationState(
        IChatMessageNotifier notifier,
        IUserDirectory userDirectory)
    {
        _notifier = notifier;
        _userDirectory = userDirectory;
        _notifier.CallRinging += HandleCallRinging;
        _notifier.CallInviteReceived += HandleCallInviteReceived;
        _notifier.CallEnded += HandleCallEnded;
        _notifier.NewMessageToast += HandleNewMessageToast;
    }

    /// <summary>
    /// Initializes the state with the current user ID so self-initiated calls
    /// are suppressed and duplicate invites for active calls are filtered.
    /// </summary>
    public void Initialize(Guid currentUserId)
    {
        _currentUserId = currentUserId;
    }

    /// <summary>
    /// Sets the active call ID so that mid-call invites for calls the user
    /// is already in are suppressed.
    /// </summary>
    public void SetActiveCallId(Guid? callId)
    {
        _activeCallId = callId;
    }

    /// <summary>
    /// Accepts the incoming call with the specified media preference.
    /// Stores a <see cref="PendingCallAccept"/> for ChatPageLayout to consume and
    /// raises <see cref="OnCallAccepted"/>.
    /// </summary>
    public void AcceptCall(bool withVideo)
    {
        if (IncomingCallId is null) return;

        PendingAccept = new PendingCallAccept(
            IncomingCallId.Value,
            IncomingCallChannelId,
            IncomingCallInitiatorId,
            withVideo);

        DismissIncomingCall();
        OnCallAccepted?.Invoke();
        OnChange?.Invoke();
    }

    /// <summary>
    /// Dismisses the incoming call notification.
    /// The caller is responsible for invoking the rejection API if needed.
    /// </summary>
    public void DismissNotification()
    {
        DismissIncomingCall();
        OnChange?.Invoke();
    }

    /// <summary>
    /// Consumes and returns the pending call accept, or <c>null</c> if none exists.
    /// After this call, <see cref="PendingAccept"/> is cleared.
    /// </summary>
    public PendingCallAccept? ConsumePendingAccept()
    {
        var pending = PendingAccept;
        PendingAccept = null;
        return pending;
    }

    private void HandleCallRinging(CallRingingNotification notification)
    {
        if (_currentUserId == Guid.Empty) return;
        if (notification.InitiatorUserId == _currentUserId) return;
        if (!notification.TargetUserIds.Contains(_currentUserId)) return;

        IncomingCallId = notification.CallId;
        IncomingCallInitiatorId = notification.InitiatorUserId;
        IncomingCallChannelId = notification.ChannelId;
        ShowIncomingCall = true;
        IsRinging = true;
        CallerName = notification.InitiatorUserId.ToString()[..8];
        ChannelName = notification.ChannelId.ToString()[..8];
        MediaType = notification.MediaType;
        IsMidCallInvite = false;
        ParticipantCount = 0;
        RemainingSeconds = 30;

        StartRingTimer();
        _ = ResolveCallerInfoAsync(notification.InitiatorUserId);
        OnChange?.Invoke();
    }

    private void HandleCallInviteReceived(CallInviteReceivedNotification notification)
    {
        if (_currentUserId == Guid.Empty) return;
        if (_activeCallId == notification.CallId) return;
        if (notification.TargetUserId != _currentUserId) return;

        IncomingCallId = notification.CallId;
        IncomingCallInitiatorId = notification.InvitedByUserId;
        IncomingCallChannelId = notification.ChannelId;
        ShowIncomingCall = true;
        IsRinging = true;
        CallerName = notification.InvitedByDisplayName
            ?? notification.InvitedByUserId.ToString()[..8];
        ChannelName = notification.ChannelId.ToString()[..8];
        MediaType = notification.MediaType;
        IsMidCallInvite = notification.IsMidCallInvite;
        ParticipantCount = notification.ParticipantCount;
        RemainingSeconds = 30;

        StartRingTimer();

        if (notification.InvitedByDisplayName is null)
        {
            _ = ResolveCallerInfoAsync(notification.InvitedByUserId);
        }

        OnChange?.Invoke();
    }

    private void HandleCallEnded(CallEndedNotification notification)
    {
        if (IncomingCallId == notification.CallId)
        {
            DismissIncomingCall();
            OnChange?.Invoke();
        }

        if (_activeCallId == notification.CallId)
        {
            _activeCallId = null;
        }
    }

    private void HandleNewMessageToast(NewMessageToastNotification notification)
    {
        if (_currentUserId == Guid.Empty) return;
        if (notification.TargetUserId != _currentUserId) return;

        ToastChannelId = notification.ChannelId;
        ToastChannelName = notification.ChannelName;
        ToastSenderName = notification.SenderName;
        ToastMessagePreview = notification.MessagePreview;
        ShowMessageToast = true;

        StartToastTimer();
        OnChange?.Invoke();
    }

    /// <summary>
    /// Dismisses the currently visible message toast notification.
    /// </summary>
    public void DismissMessageToast()
    {
        ShowMessageToast = false;
        ToastChannelId = null;
        ToastChannelName = string.Empty;
        ToastSenderName = string.Empty;
        ToastMessagePreview = string.Empty;
        StopToastTimer();
        OnChange?.Invoke();
    }

    private void DismissIncomingCall()
    {
        ShowIncomingCall = false;
        IsRinging = false;
        IncomingCallId = null;
        IncomingCallInitiatorId = null;
        IncomingCallChannelId = null;
        CallerAvatarUrl = null;
        StopRingTimer();
    }

    private void StartRingTimer()
    {
        StopRingTimer();
        _ringTimer = new System.Timers.Timer(1000);
        _ringTimer.Elapsed += (_, _) =>
        {
            RemainingSeconds--;
            if (RemainingSeconds <= 0)
            {
                DismissIncomingCall();
            }

            OnChange?.Invoke();
        };
        _ringTimer.Start();
    }

    private void StopRingTimer()
    {
        _ringTimer?.Stop();
        _ringTimer?.Dispose();
        _ringTimer = null;
    }

    private void StartToastTimer()
    {
        StopToastTimer();
        _toastTimer = new System.Timers.Timer(4000)
        {
            AutoReset = false
        };
        _toastTimer.Elapsed += (_, _) =>
        {
            ShowMessageToast = false;
            ToastChannelId = null;
            ToastChannelName = string.Empty;
            ToastSenderName = string.Empty;
            ToastMessagePreview = string.Empty;
            OnChange?.Invoke();
        };
        _toastTimer.Start();
    }

    private void StopToastTimer()
    {
        _toastTimer?.Stop();
        _toastTimer?.Dispose();
        _toastTimer = null;
    }

    private async Task ResolveCallerInfoAsync(Guid userId)
    {
        try
        {
            var names = await _userDirectory.GetDisplayNamesAsync([userId]);
            if (names.TryGetValue(userId, out var name))
            {
                CallerName = name;
                OnChange?.Invoke();
            }

            var avatars = await _userDirectory.GetAvatarUrlsAsync([userId]);
            if (avatars.TryGetValue(userId, out var url))
            {
                CallerAvatarUrl = url;
                OnChange?.Invoke();
            }
        }
        catch
        {
            // Non-critical — fallback names already set
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _notifier.CallRinging -= HandleCallRinging;
        _notifier.CallInviteReceived -= HandleCallInviteReceived;
        _notifier.CallEnded -= HandleCallEnded;
        _notifier.NewMessageToast -= HandleNewMessageToast;
        StopRingTimer();
        StopToastTimer();
    }
}

/// <summary>
/// Represents a call that was accepted from the global notification overlay
/// and is pending WebRTC join by ChatPageLayout.
/// </summary>
public sealed record PendingCallAccept(
    Guid CallId,
    Guid? ChannelId,
    Guid? InitiatorUserId,
    bool WithVideo);

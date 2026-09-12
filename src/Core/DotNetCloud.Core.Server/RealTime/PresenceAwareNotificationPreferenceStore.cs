using DotNetCloud.Modules.Chat.Services;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// <see cref="INotificationPreferenceStore"/> decorator registered only in Core.Server that
/// bridges do-not-disturb toggles into <see cref="PresenceService"/> so a DND user shows red
/// to peers immediately (no waiting for the 30 s sweep).
/// </summary>
/// <remarks>
/// Both DND entry points — the Blazor in-process toggle and <c>PUT /api/v1/notifications/preferences</c>
/// (Android/DM action) — funnel through <see cref="Update"/>; this wrapper detects a DoNotDisturb
/// flip and pushes it into the presence state engine.
/// </remarks>
internal sealed class PresenceAwareNotificationPreferenceStore : INotificationPreferenceStore
{
    private readonly INotificationPreferenceStore _inner;
    private readonly PresenceService _presenceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresenceAwareNotificationPreferenceStore"/> class.
    /// </summary>
    /// <param name="inner">The underlying preference store (DB-backed).</param>
    /// <param name="presenceService">The presence state engine to notify on DND flips.</param>
    public PresenceAwareNotificationPreferenceStore(
        INotificationPreferenceStore inner,
        PresenceService presenceService)
    {
        _inner = inner;
        _presenceService = presenceService;
    }

    /// <inheritdoc />
    public UserNotificationPreferences Get(Guid userId)
    {
        return _inner.Get(userId);
    }

    /// <inheritdoc />
    public void Update(Guid userId, UserNotificationPreferences preferences)
    {
        var previous = _inner.Get(userId).DoNotDisturb;
        _inner.Update(userId, preferences);

        if (previous != preferences.DoNotDisturb)
        {
            _presenceService.SetDoNotDisturb(userId, preferences.DoNotDisturb);
        }
    }
}

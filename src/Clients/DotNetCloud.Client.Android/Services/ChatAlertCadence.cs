namespace DotNetCloud.Client.Android.Services;

/// <summary>The inputs that decide how soon the next background chat-alert poll should run.</summary>
/// <param name="HasSession">Whether a server connection is saved at all.</param>
/// <param name="HasUnread">Whether unmuted unread messages are outstanding.</param>
/// <param name="IsDozing">Whether the device is currently in Doze.</param>
public readonly record struct ChatAlertPollState(bool HasSession, bool HasUnread, bool IsDozing);

/// <summary>
/// Cadence policy for the background chat-alert poll (<c>docs/ANDROID_UNIFIEDPUSH_PLAN.md</c> §12.9).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately pure and Android-free so the policy is unit-testable on plain <c>net10.0</c>; the
/// platform half only turns the resolved delay into a <c>JobScheduler</c> job or an alarm.
/// </para>
/// <para>
/// <b>Why these three intervals.</b> A cached app process is frozen and cannot receive socket data, and
/// Android does not deliver screen-on / unlock broadcasts to manifest-registered receivers — so while the
/// app is closed, this cadence <i>is</i> the delivery latency, not a screen-state trigger. While the
/// device dozes, ordinary jobs are deferred arbitrarily, so the Doze interval uses an allow-while-idle
/// alarm, which the platform clamps to roughly one firing per 9 minutes per app.
/// </para>
/// </remarks>
public static class ChatAlertCadence
{
    /// <summary>Interval used while unmuted unread messages are outstanding.</summary>
    public static readonly TimeSpan Active = TimeSpan.FromSeconds(60);

    /// <summary>Interval used once nothing is unread.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(5);

    /// <summary>Interval used while the device is dozing (the platform's floor for allow-while-idle alarms).</summary>
    public static readonly TimeSpan Dozing = TimeSpan.FromMinutes(9);

    /// <summary>
    /// Resolves the delay until the next poll.
    /// </summary>
    /// <param name="state">Current poll state.</param>
    /// <returns>The delay, or null when there is nothing worth scheduling (no saved session).</returns>
    public static TimeSpan? ResolveNextDelay(ChatAlertPollState state)
    {
        if (!state.HasSession)
            return null;

        if (state.IsDozing)
            return Dozing;

        return state.HasUnread ? Active : Idle;
    }

    /// <summary>
    /// Whether the next poll needs an allow-while-idle alarm rather than an ordinary job.
    /// </summary>
    /// <remarks>
    /// Only used when the user has granted the exact-alarm special access; without it the job stays the
    /// sole mechanism and the delay simply stretches, exactly as the calendar reminders already behave.
    /// </remarks>
    /// <param name="state">Current poll state.</param>
    /// <param name="exactAlarmPermissionGranted">Whether exact alarms are permitted for this app.</param>
    /// <returns>True when the Doze path should be used.</returns>
    public static bool RequiresExactAlarm(ChatAlertPollState state, bool exactAlarmPermissionGranted) =>
        state.HasSession && state.IsDozing && exactAlarmPermissionGranted;
}

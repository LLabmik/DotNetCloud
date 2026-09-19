namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Owns the background wake path that runs the chat-alert poll while the app is closed.
/// </summary>
/// <remarks>
/// <para>
/// The app deliberately runs <b>no</b> foreground service for chat, so nothing of ours survives the
/// process being frozen — this scheduler supplies the missing trigger using a persisted one-shot
/// <c>JobScheduler</c> job that re-arms itself, plus an allow-while-idle alarm while the device dozes.
/// </para>
/// <para>
/// Scheduling is idempotent, so it is safe to call on every process start; that also re-arms the chain if
/// an earlier run was killed before it could reschedule itself, and the persisted job survives a reboot.
/// </para>
/// </remarks>
public interface IChatAlertScheduler
{
    /// <summary>
    /// Ensures a poll is queued, unless one already is. Cancels the chain when no server connection is
    /// saved, so a signed-out device stops polling entirely.
    /// </summary>
    void Schedule();

    /// <summary>
    /// Re-arms the chain after a completed poll, using the adaptive cadence.
    /// </summary>
    /// <param name="result">The poll outcome. <see cref="ChatAlertPollOutcome.NoSession"/> ends the chain.</param>
    void ScheduleAfterRun(ChatAlertPollResult result);

    /// <summary>Removes any queued poll (both the job and the alarm).</summary>
    void Cancel();
}

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Controls the periodic background job that runs a media auto-upload scan while the app is closed.
/// </summary>
/// <remarks>
/// The media watcher is in-process only and therefore consumes no foreground-service budget, but
/// that also means nothing runs while the process is dead. The platform implementation registers a
/// <c>JobScheduler</c> job to supply the missing trigger; it is deliberately <b>not</b> a
/// foreground service so it never touches the Android 15/16 <c>dataSync</c> 24-hour budget.
/// </remarks>
public interface IBackgroundMediaSync
{
    /// <summary>Registers the background media sync job if it is not already scheduled.</summary>
    void Schedule();

    /// <summary>
    /// Schedules the next background run after one has completed, using a short interval while
    /// media is still queued and the idle interval once the queue is empty.
    /// </summary>
    /// <param name="pendingWork">True when the last scan left media still waiting to upload.</param>
    void ScheduleAfterRun(bool pendingWork);

    /// <summary>Removes the background media sync job.</summary>
    void Cancel();
}

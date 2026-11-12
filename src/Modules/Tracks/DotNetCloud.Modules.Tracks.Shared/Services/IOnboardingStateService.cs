namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Manages onboarding tour state per user.
/// Tracks which tours are completed, current step progress, and supports resume.
/// </summary>
/// <remarks>
/// The contract lives in the browser-safe shared assembly because the sandboxed module UI also needs
/// it. The browser has no <c>localStorage</c> in an opaque-origin frame, so the WebAssembly UI
/// supplies an in-memory implementation while the server keeps the persisted one.
/// </remarks>
public interface IOnboardingStateService
{
    /// <summary>
    /// Returns true if the user has completed the given tour.
    /// </summary>
    ValueTask<bool> IsCompletedAsync(string userId, string tourId);

    /// <summary>
    /// Returns the last saved step index (0-based) for the user + tour,
    /// or 0 if no progress has been saved.
    /// </summary>
    ValueTask<int> GetCurrentStepAsync(string userId, string tourId);

    /// <summary>
    /// Persists the current step index for the user + tour.
    /// </summary>
    ValueTask SetStepAsync(string userId, string tourId, int step);

    /// <summary>
    /// Marks the tour as completed for the user.
    /// </summary>
    ValueTask MarkCompletedAsync(string userId, string tourId);

    /// <summary>
    /// Clears all onboarding state for the user (used for "Restart tour").
    /// </summary>
    ValueTask ResetAsync(string userId, string tourId);
}

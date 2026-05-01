using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Manages onboarding tour state per user via localStorage.
/// Tracks which tours are completed, current step progress, and supports resume.
/// </summary>
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

/// <inheritdoc />
public class OnboardingStateService : IOnboardingStateService
{
    private readonly IJSRuntime _js;

    private const string KeyPrefix = "tracks_onboarding";

    public OnboardingStateService(IJSRuntime js)
    {
        _js = js;
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsCompletedAsync(string userId, string tourId)
    {
        var key = $"{KeyPrefix}_{userId}_{tourId}_completed";
        try
        {
            var val = await _js.InvokeAsync<string>("localStorage.getItem", key);
            return val == "true";
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> GetCurrentStepAsync(string userId, string tourId)
    {
        var key = $"{KeyPrefix}_{userId}_{tourId}_step";
        try
        {
            var val = await _js.InvokeAsync<string>("localStorage.getItem", key);
            return int.TryParse(val, out var step) ? step : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public async ValueTask SetStepAsync(string userId, string tourId, int step)
    {
        var key = $"{KeyPrefix}_{userId}_{tourId}_step";
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, step.ToString());
        }
        catch
        {
            // localStorage may not be available (e.g., in testing)
        }
    }

    /// <inheritdoc />
    public async ValueTask MarkCompletedAsync(string userId, string tourId)
    {
        var key = $"{KeyPrefix}_{userId}_{tourId}_completed";
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, "true");
            // Also clear the step tracking
            var stepKey = $"{KeyPrefix}_{userId}_{tourId}_step";
            await _js.InvokeVoidAsync("localStorage.removeItem", stepKey);
        }
        catch
        {
            // localStorage may not be available
        }
    }

    /// <inheritdoc />
    public async ValueTask ResetAsync(string userId, string tourId)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", $"{KeyPrefix}_{userId}_{tourId}_completed");
            await _js.InvokeVoidAsync("localStorage.removeItem", $"{KeyPrefix}_{userId}_{tourId}_step");
        }
        catch
        {
            // localStorage may not be available
        }
    }
}

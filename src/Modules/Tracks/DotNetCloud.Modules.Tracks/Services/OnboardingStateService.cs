using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.Services;

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

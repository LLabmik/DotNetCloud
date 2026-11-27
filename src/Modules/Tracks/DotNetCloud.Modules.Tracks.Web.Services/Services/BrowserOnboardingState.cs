using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// In-memory onboarding state for the sandboxed module UI.
/// </summary>
/// <remarks>
/// The server-side implementation persists to <c>localStorage</c>. A module frame has an opaque
/// origin and no <c>localStorage</c> at all, so this keeps the state for the lifetime of the page:
/// a tour resumes across re-renders, and restarting the page restarts the tour. That is the honest
/// behaviour — the alternative (silently failing writes) would make "completed" state lie.
/// </remarks>
public sealed class BrowserOnboardingState : IOnboardingStateService
{
    private readonly Dictionary<string, bool> _completed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _steps = new(StringComparer.Ordinal);
    private readonly ILogger<BrowserOnboardingState> _logger;

    /// <summary>Initializes a new instance of the <see cref="BrowserOnboardingState"/> class.</summary>
    /// <param name="logger">Logger.</param>
    public BrowserOnboardingState(ILogger<BrowserOnboardingState> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<bool> IsCompletedAsync(string userId, string tourId)
        => ValueTask.FromResult(_completed.TryGetValue(Key(userId, tourId), out var done) && done);

    /// <inheritdoc />
    public ValueTask<int> GetCurrentStepAsync(string userId, string tourId)
        => ValueTask.FromResult(_steps.TryGetValue(Key(userId, tourId), out var step) ? step : 0);

    /// <inheritdoc />
    public ValueTask SetStepAsync(string userId, string tourId, int step)
    {
        _steps[Key(userId, tourId)] = step;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask MarkCompletedAsync(string userId, string tourId)
    {
        _completed[Key(userId, tourId)] = true;
        _steps.Remove(Key(userId, tourId));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResetAsync(string userId, string tourId)
    {
        _completed.Remove(Key(userId, tourId));
        _steps.Remove(Key(userId, tourId));
        _logger.LogDebug("Onboarding tour {TourId} reset for the current page session", tourId);
        return ValueTask.CompletedTask;
    }

    private static string Key(string userId, string tourId) => $"{userId}:{tourId}";
}

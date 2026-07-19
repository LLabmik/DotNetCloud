namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Default implementation of <see cref="IAppForegroundService"/>.
/// Updated by MainActivity lifecycle callbacks (OnResume/OnPause).
/// </summary>
internal sealed class AppForegroundService : IAppForegroundService
{
    private volatile bool _isInForeground;

    /// <inheritdoc />
    public bool IsInForeground => _isInForeground;

    /// <inheritdoc />
    public event EventHandler<bool>? ForegroundChanged;

    /// <summary>
    /// Called by MainActivity.OnResume and MainActivity.OnPause.
    /// Thread-safe; fires <see cref="ForegroundChanged"/> on the calling thread.
    /// </summary>
    public void SetForeground(bool isInForeground)
    {
        if (_isInForeground == isInForeground)
            return;

        _isInForeground = isInForeground;
        ForegroundChanged?.Invoke(this, isInForeground);
    }
}

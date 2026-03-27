namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// Service for managing toast notifications across the UI.
/// Registered as scoped so each Blazor circuit (user session) has its own instance.
/// </summary>
public sealed class ToastService
{
    private readonly List<ToastMessage> _toasts = [];
    private int _nextId;

    /// <summary>
    /// Raised when the toast list changes.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Gets the current toast messages.
    /// </summary>
    public IReadOnlyList<ToastMessage> Toasts => _toasts;

    /// <summary>
    /// Shows a success toast.
    /// </summary>
    public void ShowSuccess(string message, int durationMs = 4000)
        => Show(message, ToastLevel.Success, durationMs);

    /// <summary>
    /// Shows an error toast.
    /// </summary>
    public void ShowError(string message, int durationMs = 6000)
        => Show(message, ToastLevel.Error, durationMs);

    /// <summary>
    /// Shows a warning toast.
    /// </summary>
    public void ShowWarning(string message, int durationMs = 5000)
        => Show(message, ToastLevel.Warning, durationMs);

    /// <summary>
    /// Shows an info toast.
    /// </summary>
    public void ShowInfo(string message, int durationMs = 4000)
        => Show(message, ToastLevel.Info, durationMs);

    /// <summary>
    /// Removes a toast by ID.
    /// </summary>
    public void Remove(int id)
    {
        var toast = _toasts.Find(t => t.Id == id);
        if (toast is not null)
        {
            _toasts.Remove(toast);
            OnChange?.Invoke();
        }
    }

    private void Show(string message, ToastLevel level, int durationMs)
    {
        var toast = new ToastMessage(Interlocked.Increment(ref _nextId), message, level, durationMs);
        _toasts.Add(toast);
        OnChange?.Invoke();

        if (durationMs > 0)
        {
            _ = Task.Delay(durationMs).ContinueWith(_ => Remove(toast.Id));
        }
    }
}

/// <summary>
/// Represents a toast notification message.
/// </summary>
public sealed record ToastMessage(int Id, string Message, ToastLevel Level, int DurationMs);

/// <summary>
/// Toast severity level.
/// </summary>
public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

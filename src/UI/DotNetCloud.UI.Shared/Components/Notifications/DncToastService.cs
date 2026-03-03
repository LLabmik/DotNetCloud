namespace DotNetCloud.UI.Shared.Components.Notifications;

/// <summary>
/// Service for managing toast notifications. Register as singleton.
/// </summary>
public sealed class DncToastService
{
    private readonly List<ToastMessage> _toasts = [];
    private int _nextId;

    /// <summary>Raised when the toast list changes.</summary>
    public event Action? OnChange;

    /// <summary>Gets the current toast messages.</summary>
    public IReadOnlyList<ToastMessage> Toasts => _toasts;

    /// <summary>Shows a success toast.</summary>
    public void ShowSuccess(string message, int durationMs = 4000)
        => Show(message, ToastLevel.Success, durationMs);

    /// <summary>Shows an error toast.</summary>
    public void ShowError(string message, int durationMs = 6000)
        => Show(message, ToastLevel.Error, durationMs);

    /// <summary>Shows a warning toast.</summary>
    public void ShowWarning(string message, int durationMs = 5000)
        => Show(message, ToastLevel.Warning, durationMs);

    /// <summary>Shows an info toast.</summary>
    public void ShowInfo(string message, int durationMs = 4000)
        => Show(message, ToastLevel.Info, durationMs);

    /// <summary>Removes a toast by ID.</summary>
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
        var id = Interlocked.Increment(ref _nextId);
        var toast = new ToastMessage(id, message, level);
        _toasts.Add(toast);
        OnChange?.Invoke();

        _ = RemoveAfterDelayAsync(id, durationMs);
    }

    private async Task RemoveAfterDelayAsync(int id, int durationMs)
    {
        await Task.Delay(durationMs);
        Remove(id);
    }
}

/// <summary>A single toast notification message.</summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Message">Display text.</param>
/// <param name="Level">Severity level.</param>
public sealed record ToastMessage(int Id, string Message, ToastLevel Level);

/// <summary>Severity level for toast notifications.</summary>
public enum ToastLevel
{
    /// <summary>Informational.</summary>
    Info,

    /// <summary>Success / positive.</summary>
    Success,

    /// <summary>Warning / caution.</summary>
    Warning,

    /// <summary>Error / failure.</summary>
    Error
}

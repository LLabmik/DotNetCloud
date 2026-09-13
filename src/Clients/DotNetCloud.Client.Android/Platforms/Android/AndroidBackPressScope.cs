using AndroidX.Activity;

namespace DotNetCloud.Client.Android.Platforms;

/// <summary>
/// Registers an AndroidX <see cref="OnBackPressedCallback"/> for a page so the page can consume the
/// system back press — both the physical/gesture back button and, on Android 13+, the predictive
/// back gesture.
/// </summary>
/// <remarks>
/// <para>
/// MAUI 10.0.90 only enables its own back callback when the framework has a navigation stack of its
/// own to unwind (<c>Window.CanConsumeBackNavigation</c> — modal stack, presented flyout, or a Shell
/// section stack deeper than its root). A drawer page such as the Music tab is the root of its Shell
/// section, so <c>Page.OnBackButtonPressed</c> is never consulted there and the press falls straight
/// through to the platform, which finishes the activity and drops the user out of the app even when
/// there is still in-page navigation left (e.g. an artist's album list).
/// </para>
/// <para>
/// The callback is kept disabled (<see cref="Refresh"/>) while the page has nothing to go back to,
/// so from the root of the page the system still plays its normal back-to-home animation. Android
/// reads the enabled state before the gesture commits, which is why the owner must call
/// <see cref="Refresh"/> whenever its back state changes.
/// </para>
/// </remarks>
internal sealed class AndroidBackPressScope : IDisposable
{
    private readonly Func<bool> _canHandle;
    private readonly Action _handle;
    private BackPressedCallback? _callback;
    private bool? _enabled;
    private bool _disposed;

    /// <summary>Initializes a new <see cref="AndroidBackPressScope"/>.</summary>
    /// <param name="canHandle">Returns whether the owning page can consume a back press right now.</param>
    /// <param name="handle">Invoked when a back press is handed to the page.</param>
    public AndroidBackPressScope(Func<bool> canHandle, Action handle)
    {
        _canHandle = canHandle;
        _handle = handle;
    }

    /// <summary>
    /// Registers the back callback with the current activity. Safe to call more than once — the
    /// callback is registered only once until <see cref="Dispose"/> is called.
    /// </summary>
    public void Attach() => RunOnMainThread(() =>
    {
        if (_disposed || _callback is not null)
            return;

        // The dispatcher needs the concrete activity (it owns the lifecycle/reportFullyDrawn state).
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not ComponentActivity activity)
            return;

        _callback = new BackPressedCallback(this);
        activity.OnBackPressedDispatcher.AddCallback(activity, _callback);
        Apply();
    });

    /// <summary>
    /// Re-evaluates whether the page can consume the next back press. Call this whenever the page's
    /// navigation state changes so a predictive-back gesture sees the up-to-date state. Cheap to
    /// call often: the enabled state is only written when it actually changed.
    /// </summary>
    public void Refresh() => RunOnMainThread(Apply);

    /// <inheritdoc />
    public void Dispose() => RunOnMainThread(() =>
    {
        _disposed = true;
        _enabled = null;
        _callback?.Remove();
        _callback = null;
    });

    /// <summary>Pushes the current decision to the platform callback (main thread only).</summary>
    private void Apply()
    {
        if (_callback is null)
            return;

        var enabled = _canHandle();
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        _callback.Enabled = enabled;
    }

    /// <summary>
    /// Runs <paramref name="action"/> inline when already on the main thread (the common case for
    /// page lifecycle and bound-property changes) and otherwise posts it there, because AndroidX
    /// only allows registering and toggling back callbacks from the main thread.
    /// </summary>
    private static void RunOnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
            action();
        else
            MainThread.BeginInvokeOnMainThread(action);
    }

    /// <summary>Bridges a dispatched back press to the owning page.</summary>
    private sealed class BackPressedCallback : OnBackPressedCallback
    {
        private readonly AndroidBackPressScope _owner;

        /// <summary>Initializes a new, initially disabled <see cref="BackPressedCallback"/>.</summary>
        /// <param name="owner">The scope this callback belongs to.</param>
        public BackPressedCallback(AndroidBackPressScope owner)
            : base(false)
        {
            _owner = owner;
        }

        /// <inheritdoc />
        public override void HandleOnBackPressed() => _owner._handle();
    }
}

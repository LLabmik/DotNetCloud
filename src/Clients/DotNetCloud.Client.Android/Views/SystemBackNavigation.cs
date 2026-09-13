using System.ComponentModel;
using DotNetCloud.Client.Android.Platforms;

namespace DotNetCloud.Client.Android.Views;

/// <summary>
/// Connects a Shell drawer page's in-page back affordances to the Android system back press
/// (physical back button and, on Android 13+, the predictive-back gesture).
/// </summary>
/// <remarks>
/// <para>
/// MAUI 10.0.90 only enables its own Android back callback when the framework has a navigation
/// stack of its own to unwind, which is never the case for a drawer page (the root of its Shell
/// section) — see <see cref="AndroidBackPressScope"/>. Every tab with somewhere to go inside
/// itself therefore registers its own callback through this helper.
/// </para>
/// <para>
/// The callback stays disabled while the tab is hidden (another drawer item, a pushed page) and
/// while the tab has nothing to go back to, so the framework keeps ownership of those presses and
/// the system still plays its back-to-home animation from the root of the tab.
/// </para>
/// </remarks>
internal sealed class SystemBackNavigation
{
    private readonly Func<bool> _canHandle;
    private readonly Func<Task> _handle;
    private readonly AndroidBackPressScope _scope;
    private bool _isVisible;

    /// <summary>Initializes a new <see cref="SystemBackNavigation"/>.</summary>
    /// <param name="stateNotifier">
    /// The view model whose changes can change the answer to <paramref name="canHandle"/>, or
    /// <see langword="null"/> when the state lives in the page itself (call <see cref="Refresh"/>
    /// whenever it changes). Property changes only re-evaluate the scope; the scope ignores
    /// values equal to the last one, so a blanket subscription is cheap even for view models that
    /// raise high-frequency updates.
    /// </param>
    /// <param name="canHandle">Returns whether the page can consume a back press right now.</param>
    /// <param name="handle">Consumes the back press on behalf of the page.</param>
    public SystemBackNavigation(INotifyPropertyChanged? stateNotifier, Func<bool> canHandle, Func<Task> handle)
    {
        _canHandle = canHandle;
        _handle = handle;

        _scope = new AndroidBackPressScope(() => _isVisible && ShouldHandle, () => _ = RunAsync());

        if (stateNotifier is not null)
            stateNotifier.PropertyChanged += (_, _) => _scope.Refresh();
    }

    /// <summary>Call from the page's <c>OnAppearing</c>.</summary>
    public void Attach()
    {
        _isVisible = true;
        _scope.Attach();
        _scope.Refresh();
    }

    /// <summary>Call from the page's <c>OnDisappearing</c>.</summary>
    public void Detach()
    {
        _isVisible = false;
        _scope.Refresh();
    }

    /// <summary>
    /// Re-evaluates whether the page can consume the next back press, for state that is not
    /// raised by the view model.
    /// </summary>
    public void Refresh() => _scope.Refresh();

    /// <summary>
    /// True when the press belongs to this tab. Presses stacked over the tab — an open flyout
    /// drawer or a modal page — are the Shell's to handle, so the tab yields them.
    /// </summary>
    private bool ShouldHandle =>
        _canHandle()
        && Shell.Current is { } shell
        && shell.Navigation.ModalStack.Count == 0
        && !(shell.FlyoutBehavior == FlyoutBehavior.Flyout && shell.FlyoutIsPresented);

    /// <summary>Runs the page's back handling without ever letting a failure escape the handler.</summary>
    private async Task RunAsync()
    {
        try
        {
            await _handle();
        }
        catch (Exception ex)
        {
            // A back press must never take the app down: log it and leave the tab as it was.
            System.Diagnostics.Debug.WriteLine($"[SystemBack] back handling failed: {ex}");
        }
    }
}

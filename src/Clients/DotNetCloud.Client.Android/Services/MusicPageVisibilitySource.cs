using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Provides binding-friendly access to <see cref="ModuleAvailabilityState.IsMusicModuleAvailable"/>
/// with <see cref="INotifyPropertyChanged"/> support so that <c>IsVisible</c> bindings in
/// <see cref="AppShell"/> react to state changes.
/// </summary>
public sealed class MusicPageVisibilitySource : INotifyPropertyChanged
{
    /// <summary>Whether the Music module is available on the connected server.</summary>
    public bool IsMusicModuleAvailable => ModuleAvailabilityState.IsMusicModuleAvailable;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged"/> so bound UI re-evaluates visibility.</summary>
    public void Refresh() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMusicModuleAvailable)));
}

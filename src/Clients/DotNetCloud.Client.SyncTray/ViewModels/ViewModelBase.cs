using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>Base class for all view-models in the SyncTray application.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets <paramref name="field"/> to <paramref name="value"/> and raises
    /// <see cref="PropertyChanged"/> when the value actually changes.
    /// </summary>
    /// <typeparam name="T">Property value type.</typeparam>
    /// <returns><see langword="true"/> if the value changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="propertyName"/>.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Production implementation of <see cref="IAppPreferences"/> backed by
/// <see cref="Microsoft.Maui.Storage.Preferences"/>.
/// </summary>
internal sealed class MauiAppPreferences : IAppPreferences
{
    /// <inheritdoc />
    public T Get<T>(string key, T defaultValue) => Preferences.Default.Get(key, defaultValue);

    /// <inheritdoc />
    public void Set<T>(string key, T value) => Preferences.Default.Set(key, value);
}

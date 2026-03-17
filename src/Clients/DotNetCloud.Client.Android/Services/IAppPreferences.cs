namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Abstracts app preferences storage so ViewModels can be unit-tested
/// without depending on <c>Microsoft.Maui.Storage.Preferences</c>.
/// </summary>
public interface IAppPreferences
{
    /// <summary>Gets a preference value, returning <paramref name="defaultValue"/> if not set.</summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>Sets a preference value.</summary>
    void Set<T>(string key, T value);
}

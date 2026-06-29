namespace DotNetCloud.Client.Android.Services;

/// <summary>Holds the availability state of optional server modules.
/// Set by <see cref="App"/> after querying the server at startup.</summary>
public static class ModuleAvailabilityState
{
    /// <summary>Whether the Music module is installed and available on the connected server.</summary>
    public static bool IsMusicModuleAvailable { get; set; }

    /// <summary>
    /// Fired when <see cref="IsMusicModuleAvailable"/> changes.
    /// <see cref="AppShell"/> hooks this to refresh the <see cref="MusicPageVisibilitySource"/> binding.
    /// </summary>
    public static event Action? MusicAvailabilityChanged;

    /// <summary>
    /// Sets <see cref="IsMusicModuleAvailable"/> and fires <see cref="MusicAvailabilityChanged"/>.
    /// </summary>
    public static void SetMusicAvailable(bool available)
    {
        IsMusicModuleAvailable = available;
        MusicAvailabilityChanged?.Invoke();
    }
}

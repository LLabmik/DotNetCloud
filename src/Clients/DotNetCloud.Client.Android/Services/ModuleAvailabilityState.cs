using System.Collections.Generic;

namespace DotNetCloud.Client.Android.Services;

/// <summary>Holds the availability state of optional server modules.
/// Set by <c>App</c> after querying the server at startup.</summary>
public static class ModuleAvailabilityState
{
    private static readonly HashSet<string> _availableModules = new();

    /// <summary>Whether the Music module is installed and available on the connected server.</summary>
    public static bool IsMusicModuleAvailable => _availableModules.Contains("Music");

    /// <summary>Read-only set of all currently available module names.</summary>
    public static IReadOnlySet<string> AvailableModuleNames => _availableModules;

    /// <summary>
    /// Fired when <see cref="IsMusicModuleAvailable"/> changes.
    /// <c>AppShell</c> hooks this to refresh the <see cref="MusicPageVisibilitySource"/> binding.
    /// </summary>
    public static event Action? MusicAvailabilityChanged;

    /// <summary>Fired when any module's availability changes. Parameter is the module name.</summary>
    public static event Action<string>? ModuleAvailabilityChanged;

    /// <summary>
    /// Sets the availability of a named module and fires the appropriate events.
    /// </summary>
    public static void SetModuleAvailable(string moduleName, bool available)
    {
        if (available)
            _availableModules.Add(moduleName);
        else
            _availableModules.Remove(moduleName);

        ModuleAvailabilityChanged?.Invoke(moduleName);
    }

    /// <summary>
    /// Sets <see cref="IsMusicModuleAvailable"/> and fires <see cref="MusicAvailabilityChanged"/>.
    /// </summary>
    public static void SetMusicAvailable(bool available)
    {
        SetModuleAvailable("Music", available);
        MusicAvailabilityChanged?.Invoke();
    }

    /// <summary>Whether the AI module is installed and available on the connected server.</summary>
    public static bool IsAiModuleAvailable => _availableModules.Contains("AI");

    /// <summary>Fired when <see cref="IsAiModuleAvailable"/> changes.</summary>
    public static event Action? AiAvailabilityChanged;

    /// <summary>Sets <see cref="IsAiModuleAvailable"/> and fires <see cref="AiAvailabilityChanged"/>.</summary>
    public static void SetAiAvailable(bool available)
    {
        SetModuleAvailable("AI", available);
        AiAvailabilityChanged?.Invoke();
    }

    /// <summary>Returns true if the named module is currently available.</summary>
    public static bool IsModuleAvailable(string moduleName) => _availableModules.Contains(moduleName);

    /// <summary>Clears all cached module availability — used before a full rescan.</summary>
    public static void ClearAll()
    {
        _availableModules.Clear();
    }
}

using Serilog.Core;
using Serilog.Events;

namespace DotNetCloud.Core.ServiceDefaults.Logging;

/// <summary>
/// Serilog log event filter that can filter logs by module name.
/// </summary>
public class ModuleLogFilter : ILogEventFilter
{
    private readonly HashSet<string> _excludedModules;
    private readonly Dictionary<string, LogEventLevel> _moduleLevels;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleLogFilter"/> class.
    /// </summary>
    /// <param name="excludedModules">Set of module names to exclude from logging.</param>
    /// <param name="moduleLevels">Dictionary mapping module names to their minimum log levels.</param>
    public ModuleLogFilter(
        HashSet<string>? excludedModules = null,
        Dictionary<string, LogEventLevel>? moduleLevels = null)
    {
        _excludedModules = excludedModules ?? new HashSet<string>();
        _moduleLevels = moduleLevels ?? new Dictionary<string, LogEventLevel>();
    }

    /// <summary>
    /// Determines whether the log event should be included.
    /// </summary>
    /// <param name="logEvent">The log event to filter.</param>
    /// <returns>True if the event should be logged; otherwise, false.</returns>
    public bool IsEnabled(LogEvent logEvent)
    {
        // Check if ModuleName property exists
        if (!logEvent.Properties.TryGetValue("ModuleName", out var moduleNameValue))
        {
            return true; // Allow logs without module name
        }

        var moduleName = moduleNameValue.ToString().Trim('"');

        // Check if module is excluded
        if (_excludedModules.Contains(moduleName))
        {
            return false;
        }

        // Check if module has a specific log level
        if (_moduleLevels.TryGetValue(moduleName, out var minLevel))
        {
            return logEvent.Level >= minLevel;
        }

        return true;
    }
}

using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages;

/// <summary>Code-behind for the <see cref="ModuleHelp"/> page.</summary>
public partial class ModuleHelp : ComponentBase
{
    /// <summary>The module identifier from the URL (e.g., "files", "notes").</summary>
    [Parameter]
    public string Module { get; set; } = string.Empty;

    /// <summary>Module-specific help content rendered inside the shared layout.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string ModuleName => ModuleNameMap.TryGetValue(Module, out var name) ? name : Module;
    private bool _moduleFound;

    private static readonly Dictionary<string, string> ModuleNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["files"] = "Files",
        ["notes"] = "Notes",
        ["ai"] = "AI Assistant",
        ["calendar"] = "Calendar",
        ["contacts"] = "Contacts",
        ["chat"] = "Chat",
        ["bookmarks"] = "Bookmarks",
        ["email"] = "Email",
        ["music"] = "Music",
        ["photos"] = "Photos",
        ["tracks"] = "Tracks",
        ["video"] = "Video",
        ["example"] = "Example",
        ["about"] = "About",
    };

    private static readonly HashSet<string> KnownModules = [.. ModuleNameMap.Keys];

    protected override void OnInitialized()
    {
        _moduleFound = KnownModules.Contains(Module);
    }
}

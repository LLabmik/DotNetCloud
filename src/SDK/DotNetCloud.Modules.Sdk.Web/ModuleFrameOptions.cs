namespace DotNetCloud.Modules.Sdk.Web;

/// <summary>
/// Identity a module UI presents to the core shell's frame bridge.
/// </summary>
public sealed class ModuleFrameOptions
{
    /// <summary>
    /// Gets or sets the title the shell shows while the module's frame is active.
    /// </summary>
    public string Title { get; set; } = "Module";

    /// <summary>
    /// Gets or sets the bridge role: <c>app</c> for a full module page, <c>widget</c> for a home card.
    /// </summary>
    public string Role { get; set; } = "app";
}

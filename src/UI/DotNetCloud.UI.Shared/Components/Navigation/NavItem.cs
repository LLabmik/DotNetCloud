namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>
/// Represents a navigation item in the sidebar or menu.
/// </summary>
/// <param name="Label">Display text.</param>
/// <param name="Href">Navigation URL.</param>
/// <param name="Icon">Optional icon text (emoji or icon class).</param>
public sealed record NavItem(string Label, string Href, string? Icon = null);

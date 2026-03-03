namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>
/// Represents a single tab in a <see cref="DncTabs"/> component.
/// </summary>
/// <param name="Id">Unique identifier for the tab.</param>
/// <param name="Label">Display text on the tab header.</param>
public sealed record TabItem(string Id, string Label);

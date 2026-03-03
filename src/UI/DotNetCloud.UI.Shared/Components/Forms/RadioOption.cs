namespace DotNetCloud.UI.Shared.Components.Forms;

/// <summary>
/// Represents a single option in a radio button group.
/// </summary>
/// <param name="Value">The value submitted when this option is selected.</param>
/// <param name="Label">The display text for this option.</param>
public sealed record RadioOption(string Value, string Label);

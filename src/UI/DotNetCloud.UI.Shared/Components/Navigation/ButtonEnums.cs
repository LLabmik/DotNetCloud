namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>
/// Color variant for the <see cref="DncButton"/> component.
/// </summary>
public enum ButtonVariant
{
    /// <summary>Primary blue button.</summary>
    Primary,

    /// <summary>Red danger/destructive button.</summary>
    Danger,

    /// <summary>Amber warning button.</summary>
    Warning,

    /// <summary>Green success button.</summary>
    Success,

    /// <summary>Outlined button with border only.</summary>
    Outline
}

/// <summary>
/// Size variant for the <see cref="DncButton"/> component.
/// </summary>
public enum ButtonSize
{
    /// <summary>Standard button size.</summary>
    Default,

    /// <summary>Compact button size.</summary>
    Small
}

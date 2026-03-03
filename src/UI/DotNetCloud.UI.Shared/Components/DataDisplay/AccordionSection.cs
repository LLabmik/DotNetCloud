using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>
/// Represents a collapsible section inside a <see cref="DncAccordion"/> component.
/// </summary>
public sealed class AccordionSection
{
    /// <summary>Unique identifier for the section.</summary>
    public required string Id { get; init; }

    /// <summary>Header text displayed on the toggle button.</summary>
    public required string Title { get; init; }

    /// <summary>Content rendered when the section is expanded.</summary>
    public required RenderFragment Content { get; init; }
}

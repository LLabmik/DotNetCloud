using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>Code-behind for the <see cref="DncAccordion"/> component.</summary>
public partial class DncAccordion : ComponentBase
{
    private readonly HashSet<string> _openSections = [];

    /// <summary>The accordion section definitions.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<AccordionSection> Sections { get; set; } = [];

    /// <summary>Whether multiple sections can be open simultaneously.</summary>
    [Parameter]
    public bool AllowMultiple { get; set; }

    private void Toggle(string sectionId)
    {
        if (_openSections.Contains(sectionId))
        {
            _openSections.Remove(sectionId);
        }
        else
        {
            if (!AllowMultiple)
            {
                _openSections.Clear();
            }
            _openSections.Add(sectionId);
        }
    }
}

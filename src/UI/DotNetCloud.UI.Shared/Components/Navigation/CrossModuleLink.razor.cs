using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>Code-behind for the <see cref="CrossModuleLink"/> component.</summary>
public partial class CrossModuleLink : ComponentBase
{
    [Parameter]
    public required CrossModuleLinkDto Link { get; set; }

    private static string GetIcon(CrossModuleLinkType linkType)
    {
        return linkType switch
        {
            CrossModuleLinkType.Contact => "person",
            CrossModuleLinkType.CalendarEvent => "calendar_today",
            CrossModuleLinkType.Note => "edit_note",
            CrossModuleLinkType.File => "folder",
            _ => "link"
        };
    }
}

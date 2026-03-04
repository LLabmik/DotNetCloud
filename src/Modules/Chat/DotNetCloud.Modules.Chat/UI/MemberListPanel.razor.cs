using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the member list panel component.
/// Displays channel members grouped by online status and role.
/// </summary>
public partial class MemberListPanel : ComponentBase
{
    /// <summary>Members to display.</summary>
    [Parameter]
    public List<MemberViewModel> Members { get; set; } = [];

    /// <summary>Callback to close the panel.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Gets initials from a display name.</summary>
    protected static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][..1]}{parts[^1][..1]}".ToUpperInvariant()
        };
    }
}

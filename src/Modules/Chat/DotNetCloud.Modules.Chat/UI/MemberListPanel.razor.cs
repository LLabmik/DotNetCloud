using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the member list panel component.
/// Displays channel members grouped by online status and role.
/// </summary>
public partial class MemberListPanel : ComponentBase
{
    private MemberViewModel? _selectedMember;

    /// <summary>Members to display.</summary>
    [Parameter]
    public List<MemberViewModel> Members { get; set; } = [];

    /// <summary>Callback to close the panel.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Whether role/member actions are enabled.</summary>
    [Parameter]
    public bool CanManageMembers { get; set; }

    /// <summary>Callback to promote a member.</summary>
    [Parameter]
    public EventCallback<Guid> OnPromoteMember { get; set; }

    /// <summary>Callback to demote a member.</summary>
    [Parameter]
    public EventCallback<Guid> OnDemoteMember { get; set; }

    /// <summary>Callback to remove a member.</summary>
    [Parameter]
    public EventCallback<Guid> OnRemoveMember { get; set; }

    /// <summary>Currently selected member profile.</summary>
    protected MemberViewModel? SelectedMember => _selectedMember;

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

    /// <summary>Shows profile popup for a member.</summary>
    protected void ShowProfile(MemberViewModel member)
    {
        _selectedMember = member;
    }

    /// <summary>Hides profile popup.</summary>
    protected void HideProfile()
    {
        _selectedMember = null;
    }

    /// <summary>Promotes a member to admin role.</summary>
    protected async Task Promote(MemberViewModel member)
    {
        await OnPromoteMember.InvokeAsync(member.UserId);
    }

    /// <summary>Demotes a member to member role.</summary>
    protected async Task Demote(MemberViewModel member)
    {
        await OnDemoteMember.InvokeAsync(member.UserId);
    }

    /// <summary>Removes a member from the channel.</summary>
    protected async Task Remove(MemberViewModel member)
    {
        await OnRemoveMember.InvokeAsync(member.UserId);
    }
}

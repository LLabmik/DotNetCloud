using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Renders a single colored tag badge with an optional remove button.
/// </summary>
public partial class TagBadge : ComponentBase
{
    /// <summary>The tag to display.</summary>
    [Parameter, EditorRequired] public FileTagViewModel Tag { get; set; } = null!;

    /// <summary>Raised when the user clicks the remove button. Leave unset to hide the button.</summary>
    [Parameter] public EventCallback<FileTagViewModel> OnRemove { get; set; }

    private async Task HandleRemove() => await OnRemove.InvokeAsync(Tag);
}

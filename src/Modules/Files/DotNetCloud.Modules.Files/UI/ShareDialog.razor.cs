using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the share dialog component.
/// </summary>
public partial class ShareDialog : ComponentBase
{
    [Parameter] public FileNodeViewModel? Node { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    protected string ShareType { get; set; } = "User";
    protected string Permission { get; set; } = "Read";
    protected string TargetId { get; set; } = string.Empty;
    protected string LinkPassword { get; set; } = string.Empty;
    protected string Note { get; set; } = string.Empty;
    protected int MaxDownloads { get; set; }
    protected int ExpirationDays { get; set; }
    protected string GeneratedLink { get; set; } = string.Empty;

    protected void CreateShare()
    {
        if (ShareType == "PublicLink")
        {
            GeneratedLink = $"https://your-domain.com/s/{Guid.NewGuid():N}";
        }
    }

    protected void CopyLink()
    {
        // In a full implementation, use JS interop to copy to clipboard
    }
}

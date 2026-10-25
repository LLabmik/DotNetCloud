using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Files"/> page.</summary>
public partial class Files : ComponentBase
{
    [SupplyParameterFromQuery(Name = "fileId")]
    public string? FileId { get; set; }

    [SupplyParameterFromQuery(Name = "_nav")]
    public string? Nav { get; set; }

    [SupplyParameterFromQuery(Name = "sharedFolderId")]
    public string? SharedFolderId { get; set; }

    [SupplyParameterFromQuery(Name = "relativePath")]
    public string? RelativePath { get; set; }

    private Dictionary<string, object>? _parameters;

    protected override void OnParametersSet()
    {
        if (!string.IsNullOrEmpty(FileId) || !string.IsNullOrEmpty(SharedFolderId))
        {
            _parameters = new Dictionary<string, object>
            {
                ["FileIdNav"] = Nav ?? string.Empty,
                ["SharedFolderId"] = SharedFolderId ?? string.Empty,
                ["RelativePath"] = RelativePath ?? string.Empty,
            };

            if (!string.IsNullOrEmpty(FileId))
            {
                _parameters["FileId"] = FileId;
            }
        }
        else
        {
            _parameters = null;
        }
    }
}

using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Video"/> page.</summary>
public partial class Video : ComponentBase
{
    [SupplyParameterFromQuery(Name = "fileId")]
    public string? FileId { get; set; }

    [SupplyParameterFromQuery(Name = "_nav")]
    public string? Nav { get; set; }

    private Dictionary<string, object>? _parameters;

    protected override void OnParametersSet()
    {
        var hasParams = false;
        _parameters = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(FileId))
        {
            _parameters["FileId"] = FileId;
            _parameters["FileIdNav"] = Nav ?? string.Empty;
            hasParams = true;
        }

        // videoId is handled directly by VideoPage via TryOpenVideoFromQueryAsync (reads query string)

        if (!hasParams)
        {
            _parameters = null;
        }
    }
}

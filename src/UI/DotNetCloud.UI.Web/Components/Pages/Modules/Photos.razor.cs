using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Photos"/> page.</summary>
public partial class Photos : ComponentBase
{
    [SupplyParameterFromQuery(Name = "photoId")]
    public string? PhotoId { get; set; }

    [SupplyParameterFromQuery(Name = "albumId")]
    public string? AlbumId { get; set; }

    private Dictionary<string, object>? _parameters;

    protected override void OnParametersSet()
    {
        var parameters = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(PhotoId))
        {
            parameters["PhotoId"] = PhotoId;
        }

        if (!string.IsNullOrEmpty(AlbumId))
        {
            parameters["AlbumId"] = AlbumId;
        }

        _parameters = parameters.Count > 0 ? parameters : null;
    }
}

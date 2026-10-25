using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using DotNetCloud.UI.Web.Components.Layout;
using DotNetCloud.UI.Web.Components.Pages;
using DotNetCloud.UI.Web.Components.Pages.Modules.Help;
using DotNetCloud.UI.Web.Components.Shared;
using DotNetCloud.UI.Web.Components.Shared.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Music"/> component.</summary>
public partial class Music : ComponentBase
{
    [SupplyParameterFromQuery(Name = "fileId")]
    public string? FileId { get; set; }

    [SupplyParameterFromQuery(Name = "_nav")]
    public string? Nav { get; set; }

    [SupplyParameterFromQuery(Name = "trackId")]
    public string? TrackId { get; set; }

    [SupplyParameterFromQuery(Name = "playing")]
    public string? Playing { get; set; }

    [SupplyParameterFromQuery(Name = "artistId")]
    public string? ArtistId { get; set; }

    [SupplyParameterFromQuery(Name = "albumId")]
    public string? AlbumId { get; set; }

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

        if (!string.IsNullOrEmpty(TrackId))
        {
            _parameters["TrackId"] = TrackId;
            hasParams = true;
        }

        if (string.Equals(Playing, "true", StringComparison.OrdinalIgnoreCase))
        {
            _parameters["ScrollToPlaying"] = "true";
            hasParams = true;
        }

        if (!string.IsNullOrEmpty(ArtistId))
        {
            _parameters["ArtistId"] = ArtistId;
            hasParams = true;
        }

        if (!string.IsNullOrEmpty(AlbumId))
        {
            _parameters["AlbumId"] = AlbumId;
            hasParams = true;
        }

        if (!hasParams)
        {
            _parameters = null;
        }
    }
}

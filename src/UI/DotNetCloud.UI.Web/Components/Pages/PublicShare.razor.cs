using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Localization;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
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

namespace DotNetCloud.UI.Web.Components.Pages;

/// <summary>Code-behind for the <see cref="PublicShare"/> component.</summary>
public partial class PublicShare : ComponentBase
{
    [Parameter] public string Token { get; set; } = string.Empty;

    private FileShareDto? _share;
    private FileNodeDto? _node;
    private string? _error;
    private bool _loading = true;
    private bool _needsPassword;
    private string _password = string.Empty;
    private string? _passwordError;
    private bool _submittingPassword;
    private bool _resolvedWithPassword;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var info = await ShareService.GetPublicLinkInfoAsync(Token);

            if (!info.Exists)
            {
                _error = "This link is invalid, has expired, or the download limit has been reached.";
                return;
            }

            if (info.IsExpired || info.IsMaxedOut)
            {
                _error = "This link is invalid, has expired, or the download limit has been reached.";
                return;
            }

            if (info.RequiresPassword)
            {
                _needsPassword = true;
                return;
            }

            await ResolveShareAsync(null);
        }
        catch
        {
            _error = "Something went wrong loading this shared file.";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SubmitPasswordAsync()
    {
        _passwordError = null;
        _submittingPassword = true;
        StateHasChanged();

        try
        {
            await ResolveShareAsync(_password);

            if (_share is null)
            {
                _passwordError = "Incorrect password. Please try again.";
            }
            else
            {
                _needsPassword = false;
                _resolvedWithPassword = true;
            }
        }
        catch
        {
            _passwordError = "Something went wrong. Please try again.";
        }
        finally
        {
            _submittingPassword = false;
        }
    }

    private async Task ResolveShareAsync(string? password)
    {
        _share = await ShareService.ResolvePublicLinkAsync(Token, password);

        if (_share is null)
        {
            if (password is null)
                _error = "This link is invalid, has expired, or the download limit has been reached.";
            return;
        }

        var caller = CallerContext.CreateSystemContext();
        _node = await FileService.GetNodeAsync(_share.FileNodeId, caller);

        if (_node is null)
        {
            _error = "The shared file no longer exists.";
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

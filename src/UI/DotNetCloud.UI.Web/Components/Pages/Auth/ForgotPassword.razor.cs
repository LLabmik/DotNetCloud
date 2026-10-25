using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.Data.Entities.Identity;
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
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Pages.Auth;

/// <summary>Code-behind for the <see cref="ForgotPassword"/> component.</summary>
public partial class ForgotPassword : ComponentBase
{
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private ILogger<ForgotPassword> Logger { get; set; } = default!;

    [SupplyParameterFromForm]
    private ForgotPasswordModel? _model { get; set; }

    private bool _submitting;
    private bool _submitted;
    private bool _noEmailOnFile;

    protected override void OnInitialized()
    {
        _model ??= new();
    }

    private async Task SubmitAsync()
    {
        _model ??= new();
        _noEmailOnFile = false;
        _submitting = true;
        try
        {
            var user = await UserManager.FindByNameAsync(_model.Identifier)
                ?? await UserManager.FindByEmailAsync(_model.Identifier);

            if (user is not null && !string.IsNullOrEmpty(user.Email))
            {
                var token = await UserManager.GeneratePasswordResetTokenAsync(user);
                Logger.LogInformation("Password reset token generated for {Identifier}", _model.Identifier);
            }
            else if (user is not null && string.IsNullOrEmpty(user.Email))
            {
                // Account exists but has no email on file — cannot deliver a reset link.
                _noEmailOnFile = true;
            }

            _submitted = true;
        }
        finally
        {
            _submitting = false;
        }
    }

    private sealed class ForgotPasswordModel
    {
        public string Identifier { get; set; } = string.Empty;
    }
}

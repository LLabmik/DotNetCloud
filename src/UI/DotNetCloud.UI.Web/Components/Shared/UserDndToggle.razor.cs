using System.Security.Claims;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="UserDndToggle"/> component.</summary>
public partial class UserDndToggle : ComponentBase
{
    private bool _isLoaded;
    private bool _isDnd;
    private Guid _userId;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? authState.User.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdClaim, out _userId))
        {
            var prefs = NotificationPrefs.Get(_userId);
            _isDnd = prefs.DoNotDisturb;
            _isLoaded = true;
        }
    }

    private async Task ToggleDndAsync()
    {
        if (_userId == Guid.Empty)
            return;

        _isDnd = !_isDnd;
        NotificationPrefs.Update(_userId, NotificationPrefs.Get(_userId) with { DoNotDisturb = _isDnd });

        await InvokeAsync(StateHasChanged);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="DemoBanner"/> component.</summary>
public partial class DemoBanner : ComponentBase
{
    private bool _showBanner;
    private int _daysRemaining;
    private DateTime? _expiresAt;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
                    ?? user.FindFirst("sub");

                if (userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    var userDto = await ApiClient.GetUserAsync(userId);
                    if (userDto?.IsDemoUser == true)
                    {
                        _showBanner = true;
                        _expiresAt = userDto.DemoExpiresAt;
                        _daysRemaining = _expiresAt.HasValue
                            ? Math.Max(0, (int)(_expiresAt.Value - DateTime.UtcNow).TotalDays)
                            : 0;
                    }
                }
            }
        }
        catch
        {
            // If we can't determine demo status, don't show the banner
            _showBanner = false;
        }
    }
}

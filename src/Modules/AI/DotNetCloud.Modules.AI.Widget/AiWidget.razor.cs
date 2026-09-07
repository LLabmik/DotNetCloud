using System.Security.Claims;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.AI.Widget;

/// <summary>
/// Home-page widget for the AI module: conversation count and last activity time
/// for the signed-in user (conversation titles are intentionally not exposed).
/// </summary>
public partial class AiWidget : ComponentBase
{
    [Inject] private IAiApiClient ApiClient { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private ConversationStatsDto? _stats;
    private bool _loading = true;
    private string? _error;

    private ConversationStatsDto? Stats => _stats;

    private string LastActivityText => _stats?.LastActivityAt is { } last
        ? last.ToLocalTime().ToString("MMM d, yyyy 'at' h:mm tt")
        : "No activity yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var userId = await GetUserIdAsync();
            var stats = await ApiClient.GetConversationStatsAsync(userId);
            if (stats is null)
            {
                _error = "Unable to load AI data.";
            }
            else
            {
                _stats = stats;
            }
        }
        catch (Exception)
        {
            _error = "Unable to load AI data.";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task<Guid> GetUserIdAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        return userId;
    }
}

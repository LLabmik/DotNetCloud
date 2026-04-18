using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>
/// Code-behind for the global chat notifications component.
/// Initializes the notification state with the current user and handles
/// accept/reject actions for incoming calls at the top-level layout.
/// </summary>
public partial class GlobalChatNotifications : ComponentBase, IDisposable
{
    [Inject] private GlobalChatNotificationState NotificationState { get; set; } = default!;
    [Inject] private IVideoCallService VideoCallService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private bool ShouldShowMessageToast
        => NotificationState.ShowMessageToast
           && !Navigation.Uri.Contains("/apps/chat", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? authState.User.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            NotificationState.Initialize(userId);
        }

        NotificationState.OnChange += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private Task HandleAcceptVideo()
    {
        NotificationState.AcceptCall(withVideo: true);
        NavigateToChatIfNeeded();
        return Task.CompletedTask;
    }

    private Task HandleAcceptAudio()
    {
        NotificationState.AcceptCall(withVideo: false);
        NavigateToChatIfNeeded();
        return Task.CompletedTask;
    }

    private async Task HandleReject()
    {
        var callId = NotificationState.IncomingCallId;
        NotificationState.DismissNotification();

        if (callId.HasValue)
        {
            try
            {
                var caller = await GetCallerContextAsync();
                await VideoCallService.RejectCallAsync(callId.Value, caller);
            }
            catch
            {
                // Dismiss regardless of API failure
            }
        }
    }

    private Task HandleDismissToast()
    {
        NotificationState.DismissMessageToast();
        return Task.CompletedTask;
    }

    private void NavigateToChatIfNeeded()
    {
        if (!Navigation.Uri.Contains("/apps/chat", StringComparison.OrdinalIgnoreCase))
        {
            Navigation.NavigateTo("/apps/chat");
        }
    }

    private async Task<CallerContext> GetCallerContextAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = state.User;

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        NotificationState.OnChange -= HandleStateChanged;
    }
}

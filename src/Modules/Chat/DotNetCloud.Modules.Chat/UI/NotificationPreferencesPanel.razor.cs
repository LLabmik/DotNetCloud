using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for push notification preference management.
/// Loads and saves caller-level push preferences and per-channel mute state.
/// </summary>
public partial class NotificationPreferencesPanel : ComponentBase
{
    [Inject] private ChatApiClient ChatApiClient { get; set; } = default!;

    private bool _isLoading;
    private bool _isSaving;
    private bool _pushEnabled = true;
    private bool _doNotDisturb;
    private HashSet<Guid> _mutedChannelIds = [];
    private string? _errorMessage;
    private string? _successMessage;

    /// <summary>
    /// Gets or sets the current user identifier used for preference API calls.
    /// </summary>
    [Parameter]
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets channels available for per-channel mute preferences.
    /// </summary>
    [Parameter]
    public List<ChannelViewModel> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets a callback invoked after preferences are successfully saved.
    /// </summary>
    [Parameter]
    public EventCallback<NotificationPreferencesDto> OnSaved { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadPreferencesAsync();
    }

    private async Task LoadPreferencesAsync()
    {
        if (UserId == Guid.Empty)
        {
            _errorMessage = "A valid user id is required to load notification preferences.";
            return;
        }

        _isLoading = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            var preferences = await ChatApiClient.GetNotificationPreferencesAsync(UserId);

            if (preferences is null)
            {
                _errorMessage = "Unable to load notification preferences.";
                return;
            }

            ApplyPreferences(preferences);
        }
        catch
        {
            _errorMessage = "Failed to load notification preferences.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleMutedChannel(ChangeEventArgs args, Guid channelId)
    {
        var isChecked = args.Value is bool b && b;

        if (isChecked)
        {
            _mutedChannelIds.Add(channelId);
        }
        else
        {
            _mutedChannelIds.Remove(channelId);
        }
    }

    private bool IsChannelMuted(Guid channelId)
    {
        return _mutedChannelIds.Contains(channelId);
    }

    private async Task SaveAsync()
    {
        if (UserId == Guid.Empty)
        {
            _errorMessage = "A valid user id is required to save notification preferences.";
            return;
        }

        _isSaving = true;
        _errorMessage = null;
        _successMessage = null;

        var dto = new NotificationPreferencesDto
        {
            PushEnabled = _pushEnabled,
            DoNotDisturb = _doNotDisturb,
            MutedChannelIds = [.. _mutedChannelIds]
        };

        try
        {
            var updated = await ChatApiClient.UpdateNotificationPreferencesAsync(UserId, dto);

            if (!updated)
            {
                _errorMessage = "Failed to save notification preferences.";
                return;
            }

            _successMessage = "Notification preferences saved.";

            if (OnSaved.HasDelegate)
            {
                await OnSaved.InvokeAsync(dto);
            }
        }
        catch
        {
            _errorMessage = "Failed to save notification preferences.";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void ApplyPreferences(NotificationPreferencesDto preferences)
    {
        _pushEnabled = preferences.PushEnabled;
        _doNotDisturb = preferences.DoNotDisturb;
        _mutedChannelIds = [.. preferences.MutedChannelIds];
    }
}

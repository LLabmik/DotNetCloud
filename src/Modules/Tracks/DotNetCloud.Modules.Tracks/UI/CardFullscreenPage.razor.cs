using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Fullscreen card detail page rendered when accessing a card via direct URL.
/// Handles permission checks and shows access-denied / not-found states.
/// </summary>
public partial class CardFullscreenPage : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// The card number to display, passed from the shell page route parameter.
    /// </summary>
    [Parameter, EditorRequired]
    public int CardNumber { get; set; }

    private CardDto? _card;
    private BoardDto? _board;
    private bool _isLoading = true;
    private bool _accessDenied;
    private bool _notFound;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _accessDenied = false;
        _notFound = false;

        try
        {
            _card = await ApiClient.GetCardByNumberAsync(CardNumber);
            if (_card is null)
            {
                _notFound = true;
                return;
            }

            _board = await ApiClient.GetBoardAsync(_card.BoardId);
            if (_board is null)
            {
                _accessDenied = true;
                return;
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Forbidden
                                                 or System.Net.HttpStatusCode.Unauthorized)
        {
            _accessDenied = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            _notFound = true;
        }
        catch
        {
            _accessDenied = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void NavigateBack()
    {
        Navigation.NavigateTo("/apps/tracks");
    }

    private void HandleCardUpdated(CardDto card)
    {
        _card = card;
    }

    private void HandleCardDeleted(Guid cardId)
    {
        Navigation.NavigateTo("/apps/tracks");
    }
}

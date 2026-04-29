using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Fullscreen work item detail page rendered when accessing a work item via direct URL.
/// Route: /apps/tracks/item/{ProductId}/{ItemNumber}
/// </summary>
public partial class WorkItemFullscreenPage : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// The product ID the work item belongs to.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid ProductId { get; set; }

    /// <summary>
    /// The work item number to display, passed from the shell page route parameter.
    /// </summary>
    [Parameter, EditorRequired]
    public int ItemNumber { get; set; }

    private WorkItemDto? _workItem;
    private ProductDto? _product;
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
            _workItem = await ApiClient.GetWorkItemByNumberAsync(ProductId, ItemNumber);
            if (_workItem is null)
            {
                _notFound = true;
                return;
            }

            _product = await ApiClient.GetProductAsync(_workItem.ProductId);
            if (_product is null)
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

    private void HandleWorkItemUpdated(WorkItemDto item)
    {
        _workItem = item;
    }

    private void HandleWorkItemDeleted(Guid workItemId)
    {
        Navigation.NavigateTo("/apps/tracks");
    }
}

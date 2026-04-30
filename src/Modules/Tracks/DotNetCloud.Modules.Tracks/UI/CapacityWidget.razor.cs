using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Widget showing team member capacity as horizontal bar chart.
/// Green (under 60%), yellow (60-90%), orange (90-100%), red (over 100%).
/// </summary>
public partial class CapacityWidget : ComponentBase
{
    [Parameter] public required Guid ProductId { get; set; }

    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    private ProductCapacityDto? _capacity;
    private bool _isLoading = true;

    protected override async Task OnParametersSetAsync()
    {
        await LoadCapacityAsync();
    }

    private async Task LoadCapacityAsync()
    {
        _isLoading = true;
        try
        {
            _capacity = await ApiClient.GetProductCapacityAsync(ProductId);
        }
        catch
        {
            _capacity = null;
        }
        finally
        {
            _isLoading = false;
        }
    }
}

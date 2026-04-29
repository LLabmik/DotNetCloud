using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Grid view of all products in an organization, with product creation wizard,
/// soft-delete with 30-day retention, and admin restore.
/// </summary>
public partial class ProductListView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public List<ProductDto> Products { get; set; } = [];
    [Parameter] public Guid? OrganizationId { get; set; }
    [Parameter] public EventCallback<Guid> OnProductSelected { get; set; }
    [Parameter] public EventCallback<ProductDto> OnProductCreated { get; set; }
    [Parameter] public EventCallback<Guid> OnProductDeleted { get; set; }
    [Parameter] public EventCallback<ProductDto> OnProductRestored { get; set; }

    private string _searchQuery = "";
    private bool _showCreateWizard;

    // Delete state
    private ProductDto? _deleteTarget;
    private bool _isDeleting;
    private string? _deleteError;

    // Deleted products
    private readonly List<ProductDto> _deletedProducts = [];
    private bool _showDeleted;
    private Guid? _isRestoring;
    private Guid? _isPermanentlyDeleting;
    private bool _deletedLoaded;

    private IReadOnlyList<ProductDto> FilteredProducts
    {
        get
        {
            IEnumerable<ProductDto> filtered = Products.Where(p => !p.IsArchived);

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var query = _searchQuery.Trim();
                filtered = filtered.Where(p =>
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (p.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return filtered.ToList();
        }
    }

    private void OpenCreateWizard()
    {
        _showCreateWizard = true;
    }

    private void CloseCreateWizard() => _showCreateWizard = false;

    private async Task HandleProductCreatedFromWizard(ProductDto product)
    {
        _showCreateWizard = false;
        await OnProductCreated.InvokeAsync(product);
    }

    private static string TruncateText(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "…";

    private void ConfirmDelete(ProductDto product)
    {
        _deleteTarget = product;
        _deleteError = null;
    }

    private void CancelDelete()
    {
        _deleteTarget = null;
        _deleteError = null;
    }

    private async Task DeleteProductAsync()
    {
        if (_deleteTarget is null) return;

        _isDeleting = true;
        _deleteError = null;

        try
        {
            await ApiClient.DeleteProductAsync(_deleteTarget.Id);
            await OnProductDeleted.InvokeAsync(_deleteTarget.Id);
            _deleteTarget = null;
        }
        catch (Exception ex)
        {
            _deleteError = ex.Message;
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private async Task LoadDeletedProductsAsync()
    {
        if (!OrganizationId.HasValue || _deletedLoaded) return;

        try
        {
            var deleted = await ApiClient.ListDeletedProductsAsync(OrganizationId.Value);
            _deletedProducts.Clear();
            _deletedProducts.AddRange(deleted);
            _deletedLoaded = true;
        }
        catch
        {
            // Non-critical; silently skip
        }
    }

    private async Task RestoreProductAsync(ProductDto product)
    {
        _isRestoring = product.Id;

        try
        {
            var restored = await ApiClient.RestoreProductAsync(product.Id);
            if (restored is not null)
            {
                _deletedProducts.Remove(product);
                await OnProductRestored.InvokeAsync(restored);
            }
        }
        catch
        {
            // Restore failed; user can retry
        }
        finally
        {
            _isRestoring = null;
        }
    }

    private async Task PermanentDeleteProductAsync(ProductDto product)
    {
        _isPermanentlyDeleting = product.Id;

        try
        {
            await ApiClient.PermanentDeleteProductAsync(product.Id);
            _deletedProducts.Remove(product);
        }
        catch
        {
            // Can retry
        }
        finally
        {
            _isPermanentlyDeleting = null;
        }
    }

    /// <summary>
    /// Loads deleted products list when parameters change.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        await LoadDeletedProductsAsync();
    }
}

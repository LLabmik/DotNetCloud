using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Grid view of all products in an organization, with create product dialog.
/// </summary>
public partial class ProductListView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public List<ProductDto> Products { get; set; } = [];
    [Parameter] public Guid? OrganizationId { get; set; }
    [Parameter] public EventCallback<Guid> OnProductSelected { get; set; }
    [Parameter] public EventCallback<ProductDto> OnProductCreated { get; set; }
    [Parameter] public EventCallback<Guid> OnProductDeleted { get; set; }

    private string _searchQuery = "";
    private bool _showCreateDialog;
    private bool _isCreating;
    private bool _subItemsEnabled;

    private readonly CreateProductModel _createModel = new();

    private static readonly string[] _productColors =
    [
        "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6",
        "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1"
    ];

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

    private void OpenCreateDialog()
    {
        _createModel.Name = "";
        _createModel.Description = "";
        _createModel.Color = _productColors[0];
        _subItemsEnabled = false;
        _showCreateDialog = true;
    }

    private void CloseCreateDialog() => _showCreateDialog = false;

    private async Task CreateProductAsync()
    {
        if (string.IsNullOrWhiteSpace(_createModel.Name)) return;
        if (OrganizationId is null) return;

        _isCreating = true;
        try
        {
            var dto = new CreateProductDto
            {
                Name = _createModel.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(_createModel.Description) ? null : _createModel.Description.Trim(),
                Color = _createModel.Color,
                SubItemsEnabled = _subItemsEnabled
            };

            var product = await ApiClient.CreateProductAsync(OrganizationId.Value, dto);
            if (product is not null)
            {
                await OnProductCreated.InvokeAsync(product);
            }

            _showCreateDialog = false;
        }
        finally
        {
            _isCreating = false;
        }
    }

    private static string TruncateText(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "…";

    private sealed class CreateProductModel
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Color { get; set; } = "#3b82f6";
    }
}

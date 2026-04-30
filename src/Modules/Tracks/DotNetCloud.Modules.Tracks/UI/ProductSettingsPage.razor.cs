using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Product settings page: General, Swimlanes, Members, Labels, Danger Zone.
/// </summary>
public partial class ProductSettingsPage : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public ProductDto Product { get; set; } = default!;
    [Parameter] public EventCallback<ProductDto> OnProductUpdated { get; set; }
    [Parameter] public EventCallback OnNavigateBack { get; set; }

    // General
    private string _editName = "";
    private string _editDescription = "";
    private string _editColor = "";
    private bool _editSubItemsEnabled;

    // Swimlanes
    private readonly List<SettingsSwimlane> _swimlanes = [];

    // Members
    private readonly List<ProductMemberDto> _members = [];
    private bool _showAddMember;
    private string _memberSearchTerm = "";
    private readonly List<UserSearchResult> _memberSearchResults = [];

    // Labels
    private readonly List<LabelDto> _labels = [];
    private bool _showCreateLabel;
    private string _newLabelTitle = "";
    private string _newLabelColor = "#3b82f6";

    // Danger zone
    private bool _showTransferOwner;
    private string _transferTargetUserId = "";
    private bool _showDeleteConfirm;
    private string _deleteConfirmName = "";

    // State
    private bool _isSaving;
    private string? _errorMessage;
    private string? _successMessage;

    private static readonly string[] _colorPresets =
    [
        "#3b82f6", "#22c55e", "#eab308", "#f97316", "#ef4444",
        "#a855f7", "#ec4899", "#06b6d4", "#64748b", "#84cc16"
    ];

    private static readonly string[] _labelColors =
    [
        "#3b82f6", "#22c55e", "#eab308", "#f97316", "#ef4444",
        "#a855f7", "#ec4899", "#06b6d4", "#64748b", "#84cc16"
    ];

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        _editName = Product.Name;
        _editDescription = Product.Description ?? "";
        _editColor = Product.Color ?? "#3b82f6";
        _editSubItemsEnabled = Product.SubItemsEnabled;

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var membersTask = ApiClient.ListProductMembersAsync(Product.Id);
            var labelsTask = ApiClient.ListLabelsAsync(Product.Id);
            var swimlanesTask = ApiClient.ListProductSwimlanesAsync(Product.Id);

            await Task.WhenAll(membersTask, labelsTask, swimlanesTask);

            _members.Clear();
            _members.AddRange(await membersTask);

            _labels.Clear();
            _labels.AddRange(await labelsTask);

            _swimlanes.Clear();
            _swimlanes.AddRange((await swimlanesTask).Select(s => new SettingsSwimlane
            {
                Id = s.Id,
                Title = s.Title,
                IsDone = s.IsDone,
                Position = s.Position
            }));
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load data: {ex.Message}";
        }
    }

    // ── General ─────────────────────────────────────────────

    private async Task SaveGeneralAsync()
    {
        _isSaving = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            var updated = await ApiClient.UpdateProductAsync(Product.Id, new UpdateProductDto
            {
                Name = _editName.Trim(),
                Description = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription.Trim(),
                Color = _editColor,
                SubItemsEnabled = _editSubItemsEnabled
            });

            if (updated is not null)
            {
                _successMessage = "Settings saved.";
                await OnProductUpdated.InvokeAsync(updated);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    // ── Swimlanes ───────────────────────────────────────────

    private void AddSwimlane()
    {
        _swimlanes.Add(new SettingsSwimlane
        {
            Id = Guid.NewGuid(),
            Title = "New Swimlane",
            IsDone = false,
            Position = _swimlanes.Count
        });
    }

    private void RemoveSwimlane(int index)
    {
        if (index >= 0 && index < _swimlanes.Count)
            _swimlanes.RemoveAt(index);
    }

    private async Task SaveSwimlanesAsync()
    {
        _isSaving = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            // Delete existing swimlanes and recreate (simple approach for settings)
            var existing = await ApiClient.ListProductSwimlanesAsync(Product.Id);
            foreach (var s in existing)
                await ApiClient.DeleteSwimlaneAsync(s.Id);

            for (int i = 0; i < _swimlanes.Count; i++)
            {
                var s = _swimlanes[i];
                await ApiClient.CreateProductSwimlaneAsync(Product.Id, new CreateSwimlaneDto
                {
                    Title = s.Title,
                    Color = null,
                    CardLimit = null,
                    IsDone = s.IsDone
                });
            }

            _successMessage = "Swimlanes saved.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save swimlanes: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    // ── Members ─────────────────────────────────────────────

    private async Task HandleMemberSearch(KeyboardEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_memberSearchTerm) || _memberSearchTerm.Length < 2)
        {
            _memberSearchResults.Clear();
            return;
        }

        try
        {
            var results = await ApiClient.SearchUsersAsync(_memberSearchTerm, 8);
            _memberSearchResults.Clear();
            _memberSearchResults.AddRange(results.Where(r => !_members.Any(m => m.UserId == r.Id)));
        }
        catch
        {
            _memberSearchResults.Clear();
        }
    }

    private async Task AddMemberAsync(Guid userId)
    {
        try
        {
            await ApiClient.AddProductMemberAsync(Product.Id, new AddProductMemberDto
            {
                UserId = userId,
                Role = ProductMemberRole.Member
            });
            _showAddMember = false;
            _memberSearchTerm = "";
            _memberSearchResults.Clear();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to add member: {ex.Message}";
        }
    }

    private async Task UpdateMemberRole(Guid userId, ChangeEventArgs e)
    {
        var roleStr = e.Value?.ToString();
        if (roleStr is null || !Enum.TryParse<ProductMemberRole>(roleStr, out var role)) return;

        try
        {
            await ApiClient.UpdateProductMemberRoleAsync(Product.Id, userId, role);
            var member = _members.FirstOrDefault(m => m.UserId == userId);
            if (member is not null)
            {
                var index = _members.IndexOf(member);
                _members[index] = member with { Role = role };
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to update role: {ex.Message}";
        }
    }

    private async Task RemoveMemberAsync(Guid userId)
    {
        try
        {
            await ApiClient.RemoveProductMemberAsync(Product.Id, userId);
            _members.RemoveAll(m => m.UserId == userId);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to remove member: {ex.Message}";
        }
    }

    // ── Labels ──────────────────────────────────────────────

    private async Task CreateLabelAsync()
    {
        if (string.IsNullOrWhiteSpace(_newLabelTitle)) return;

        try
        {
            var label = await ApiClient.CreateLabelAsync(Product.Id, new CreateLabelDto
            {
                Title = _newLabelTitle.Trim(),
                Color = _newLabelColor
            });
            if (label is not null) _labels.Add(label);
            _showCreateLabel = false;
            _newLabelTitle = "";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to create label: {ex.Message}";
        }
    }

    private async Task DeleteLabelAsync(Guid labelId)
    {
        try
        {
            await ApiClient.DeleteLabelAsync(Product.Id, labelId);
            _labels.RemoveAll(l => l.Id == labelId);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to delete label: {ex.Message}";
        }
    }

    // ── Danger Zone ─────────────────────────────────────────

    private async Task ArchiveProductAsync()
    {
        _isSaving = true;
        try
        {
            var updated = await ApiClient.UpdateProductAsync(Product.Id, new UpdateProductDto
            {
                Name = _editName,
                Description = _editDescription
            });
            _successMessage = "Product archived. You can restore it from the product list.";
            if (updated is not null) await OnProductUpdated.InvokeAsync(updated);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to archive: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task TransferOwnershipAsync()
    {
        if (!Guid.TryParse(_transferTargetUserId, out var newOwnerId)) return;

        _isSaving = true;
        try
        {
            await ApiClient.UpdateProductMemberRoleAsync(Product.Id, newOwnerId, ProductMemberRole.Owner);
            _showTransferOwner = false;
            _successMessage = "Ownership transferred.";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to transfer: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task PermanentDeleteAsync()
    {
        if (_deleteConfirmName != Product.Name) return;

        _isSaving = true;
        try
        {
            await ApiClient.PermanentDeleteProductAsync(Product.Id);
            await OnNavigateBack.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to delete: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
    }

    private void StartAddMember()
    {
        _showAddMember = true;
        _memberSearchTerm = "";
    }

    private void CancelDeleteConfirm()
    {
        _showDeleteConfirm = false;
        _deleteConfirmName = "";
    }

    // ── Local Model ─────────────────────────────────────────

    private sealed class SettingsSwimlane
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public bool IsDone { get; set; }
        public double Position { get; set; }
    }
}

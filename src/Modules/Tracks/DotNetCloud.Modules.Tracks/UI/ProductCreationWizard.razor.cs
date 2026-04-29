using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Multi-step wizard for creating a new Product.
/// Steps: Name &amp; Description → Color &amp; Settings → Team Members → Review &amp; Create.
/// </summary>
public partial class ProductCreationWizard : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private IUserDirectory UserDirectory { get; set; } = default!;

    [Parameter] public Guid OrganizationId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<ProductDto> OnCreated { get; set; }

    private static readonly string[] _stepLabels = ["Name & Description", "Color & Settings", "Team Members", "Review"];

    private static readonly string[] _productColors =
    [
        "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6",
        "#ec4899", "#06b6d4", "#f97316", "#6366f1", "#14b8a6",
        "#e11d48", "#7c3aed", "#0ea5e9", "#84cc16", "#d946ef"
    ];

    private int _currentStep;

    // Step 1: Name & Description
    private string _name = "";
    private string _description = "";

    // Step 2: Color & Settings
    private string _color = "#3b82f6";
    private bool _subItemsEnabled;
    private readonly List<SwimlanePreset> _defaultSwimlanes = [];
    private bool _showSwimlaneSetup;

    // Step 3: Team Members
    private string _memberSearchTerm = "";
    private readonly List<UserSearchResult> _searchResults = [];
    private readonly List<UserSearchResult> _selectedMembers = [];
    private bool _isSearching;

    // State
    private bool _isSubmitting;
    private string? _errorMessage;

    private sealed class SwimlanePreset
    {
        public string Title { get; set; } = "";
        public string? Color { get; set; }
        public bool IsDone { get; set; }
    }

    protected override void OnInitialized()
    {
        _defaultSwimlanes.AddRange([
            new() { Title = "To Do", Color = "#6b7280" },
            new() { Title = "In Progress", Color = "#3b82f6" },
            new() { Title = "Done", Color = "#10b981", IsDone = true }
        ]);
    }

    private bool IsStepValid(int step) => step switch
    {
        0 => !string.IsNullOrWhiteSpace(_name),
        1 => true,
        2 => true,
        3 => true,
        _ => false
    };

    private void GoToStep(int step)
    {
        if (step <= _currentStep || IsStepValid(_currentStep))
            _currentStep = step;
    }

    private void NextStep()
    {
        if (IsStepValid(_currentStep) && _currentStep < _stepLabels.Length - 1)
            _currentStep++;
    }

    private void PreviousStep()
    {
        if (_currentStep > 0)
            _currentStep--;
    }

    private void UpdateDefaultSwimlaneTitle(int index, string value)
    {
        if (index >= 0 && index < _defaultSwimlanes.Count)
            _defaultSwimlanes[index].Title = value;
    }

    private void ToggleDefaultSwimlaneDone(int index)
    {
        if (index >= 0 && index < _defaultSwimlanes.Count)
            _defaultSwimlanes[index].IsDone = !_defaultSwimlanes[index].IsDone;
    }

    private void AddDefaultSwimlane()
    {
        _defaultSwimlanes.Add(new() { Title = $"Column {_defaultSwimlanes.Count + 1}" });
    }

    private void RemoveDefaultSwimlane(int index)
    {
        if (_defaultSwimlanes.Count > 1 && index >= 0 && index < _defaultSwimlanes.Count)
            _defaultSwimlanes.RemoveAt(index);
    }

    private async Task SearchMembersAsync()
    {
        if (string.IsNullOrWhiteSpace(_memberSearchTerm) || _memberSearchTerm.Length < 2) return;

        _isSearching = true;
        try
        {
            var results = await UserDirectory.SearchUsersAsync(_memberSearchTerm, 10);
            _searchResults.Clear();
            _searchResults.AddRange(results.Where(r => _selectedMembers.All(s => s.Id != r.Id)));
        }
        finally
        {
            _isSearching = false;
        }
    }

    private async Task HandleSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await SearchMembersAsync();
    }

    private void AddMember(UserSearchResult user)
    {
        if (_selectedMembers.All(m => m.Id != user.Id))
        {
            _selectedMembers.Add(user);
            _searchResults.Remove(user);
            _memberSearchTerm = "";
        }
    }

    private void RemoveMember(UserSearchResult user)
    {
        _selectedMembers.Remove(user);
    }

    private async Task SubmitAsync()
    {
        if (!IsStepValid(0)) return;

        _isSubmitting = true;
        _errorMessage = null;

        try
        {
            var dto = new CreateProductDto
            {
                Name = _name.Trim(),
                Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                Color = _color,
                SubItemsEnabled = _subItemsEnabled
            };

            var product = await ApiClient.CreateProductAsync(OrganizationId, dto);

            if (product is not null)
            {
                // Create default swimlanes if setup was shown
                if (_showSwimlaneSetup)
                {
                    foreach (var sw in _defaultSwimlanes.Where(s => !string.IsNullOrWhiteSpace(s.Title)))
                    {
                        await ApiClient.CreateProductSwimlaneAsync(product.Id, new CreateSwimlaneDto
                        {
                            Title = sw.Title.Trim(),
                            Color = sw.Color,
                            IsDone = sw.IsDone
                        });
                    }
                }

                // Add selected team members
                foreach (var member in _selectedMembers)
                {
                    try
                    {
                        await ApiClient.AddProductMemberAsync(product.Id, new AddProductMemberDto
                        {
                            UserId = member.Id,
                            Role = ProductMemberRole.Member
                        });
                    }
                    catch
                    {
                        // Non-fatal if member add fails
                    }
                }

                await HandleCreated(product);
            }
            else
            {
                _errorMessage = "Failed to create product.";
                _isSubmitting = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            _isSubmitting = false;
        }
    }

    public bool CanSubmit => IsStepValid(0) && !_isSubmitting;

    private async Task HandleOverlayClick() => await OnClose.InvokeAsync();
    private async Task HandleCancel() => await OnClose.InvokeAsync();
    private async Task HandleCreated(ProductDto product) => await OnCreated.InvokeAsync(product);
}

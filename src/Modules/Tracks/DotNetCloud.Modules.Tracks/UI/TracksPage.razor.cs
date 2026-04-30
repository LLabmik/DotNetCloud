using System.Security.Claims;
using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Main entry component for the Tracks module UI with drill-down hierarchy navigation.
/// Product Kanban → Epic Kanban → Feature Kanban → Item Detail.
/// </summary>
public partial class TracksPage : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private ITracksSignalRService SignalRService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IOrganizationDirectory OrgDirectory { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private enum TracksView { ProductList, ProductKanban, EpicKanban, FeatureKanban, Teams, Planning, Wizard, Backlog, Timeline, Review, Settings, Calendar }

    private TracksView _view = TracksView.ProductList;
    private bool _sidebarCollapsed;
    private bool _isLoading = true;
    private string? _errorMessage;

    // Organization state
    private readonly List<OrganizationDto> _organizations = [];
    private Guid? _selectedOrgId;

    // Product state
    private readonly List<ProductDto> _products = [];
    private ProductDto? _selectedProduct;
    private readonly List<SwimlaneDto> _currentSwimlanes = [];
    private readonly Dictionary<Guid, List<WorkItemDto>> _currentWorkItems = [];
    private readonly List<LabelDto> _currentLabels = [];
    private readonly List<ProductMemberDto> _currentMembers = [];

    // Drill-down state
    private WorkItemDto? _selectedEpic;
    private WorkItemDto? _selectedFeature;
    private WorkItemDto? _selectedWorkItem;

    // Teams
    private readonly List<TracksTeamDto> _teams = [];

    // Sprint state
    private readonly List<SprintDto> _sprints = [];
    private bool _showSprints;

    // Panels
    private SprintDto? _planningSprint;

    // Review session
    private ReviewSessionDto? _activeReviewSession;
    private bool _isHost;
    private bool _isStartingReview;
    private string? _reviewStartError;

    // Keyboard shortcuts
    private bool _showShortcutsModal;
    private DotNetObjectReference<TracksPage>? _jsRef;

    // Undo toast
    private UndoToast? _undoToast;
    private string _undoToastMessage = "";
    private Guid? _lastDeletedProductId;

    // Saved views
    private CustomViewsSidebar? _customViewsSidebar;
    private Guid? _selectedViewId;
    private bool _showSaveViewDialog;
    private string _newViewName = "";
    private bool _newViewIsShared;

    private SprintDto? PlannableSprint => _sprints.FirstOrDefault(s => s.Status is SprintStatus.Planning or SprintStatus.Active);

    protected override async Task OnInitializedAsync()
    {
        SubscribeToRealtimeEvents();
        await LoadInitialDataAsync();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("tracksKeyboard.init", _jsRef);
        }
    }

    private async Task LoadInitialDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId.HasValue)
            {
                var orgs = await OrgDirectory.GetUserOrganizationsAsync(userId.Value);
                _organizations.Clear();
                _organizations.AddRange(orgs);
            }

            var teamsTask = ApiClient.ListTeamsAsync();
            _teams.Clear();
            _teams.AddRange(await teamsTask);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load data: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnOrgSelected()
    {
        if (_selectedOrgId.HasValue)
            await SelectOrganization(_selectedOrgId.Value);
    }

    private async Task<Guid?> GetCurrentUserIdAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        var claim = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? state.User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    // ── Navigation ──────────────────────────────────────────

    private async Task SelectOrganization(Guid orgId)
    {
        _selectedOrgId = orgId;
        _selectedProduct = null;
        _selectedEpic = null;
        _selectedFeature = null;
        _selectedWorkItem = null;
        _view = TracksView.ProductList;

        try
        {
            var products = await ApiClient.ListProductsAsync(orgId);
            _products.Clear();
            _products.AddRange(products);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load products: {ex.Message}";
        }
    }

    private async Task SelectProduct(Guid productId)
    {
        _isLoading = true;
        _errorMessage = null;
        _selectedEpic = null;
        _selectedFeature = null;
        _selectedWorkItem = null;

        try
        {
            _selectedProduct = await ApiClient.GetProductAsync(productId);
            if (_selectedProduct is null)
            {
                _errorMessage = "Product not found.";
                return;
            }

            await LoadProductKanbanDataAsync();
            _view = TracksView.ProductKanban;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load product: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OpenEpicKanban(Guid epicId)
    {
        _isLoading = true;
        _selectedFeature = null;
        _selectedWorkItem = null;

        try
        {
            _selectedEpic = await ApiClient.GetWorkItemAsync(epicId);
            if (_selectedEpic is null) return;

            await LoadEpicKanbanDataAsync();
            _view = TracksView.EpicKanban;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load epic: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OpenFeatureKanban(Guid featureId)
    {
        _isLoading = true;
        _selectedWorkItem = null;

        try
        {
            _selectedFeature = await ApiClient.GetWorkItemAsync(featureId);
            if (_selectedFeature is null) return;

            await LoadFeatureKanbanDataAsync();
            _view = TracksView.FeatureKanban;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load feature: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SelectWorkItem(Guid workItemId)
    {
        try
        {
            _selectedWorkItem = await ApiClient.GetWorkItemAsync(workItemId);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load work item: {ex.Message}";
        }
    }

    /// <summary>Selects a work item by its product-scoped item number (used by calendar view).</summary>
    private async Task SelectWorkItemByNumber(int itemNumber)
    {
        if (_selectedProduct is null) return;
        try
        {
            _selectedWorkItem = await ApiClient.GetWorkItemByNumberAsync(_selectedProduct.Id, itemNumber);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load work item: {ex.Message}";
        }
    }

    private void CloseWorkItemDetail()
    {
        _selectedWorkItem = null;
    }

    // ── Kanban Data Loading ─────────────────────────────────

    private async Task LoadProductKanbanDataAsync()
    {
        if (_selectedProduct is null) return;

        var swimlanes = await ApiClient.ListProductSwimlanesAsync(_selectedProduct.Id);
        var labels = await ApiClient.ListLabelsAsync(_selectedProduct.Id);
        var members = await ApiClient.ListProductMembersAsync(_selectedProduct.Id);

        _currentSwimlanes.Clear();
        _currentSwimlanes.AddRange(swimlanes.OrderBy(s => s.Position));

        _currentLabels.Clear();
        _currentLabels.AddRange(labels);

        _currentMembers.Clear();
        _currentMembers.AddRange(members);

        _currentWorkItems.Clear();
        foreach (var swimlane in _currentSwimlanes)
        {
            var items = await ApiClient.ListWorkItemsAsync(swimlane.Id);
            _currentWorkItems[swimlane.Id] = items.OrderBy(c => c.Position).ToList();
        }
    }

    private async Task LoadEpicKanbanDataAsync()
    {
        if (_selectedEpic is null) return;
        if (_selectedProduct is null) return;

        var swimlanes = await ApiClient.ListWorkItemSwimlanesAsync(_selectedEpic.Id);
        _currentSwimlanes.Clear();
        _currentSwimlanes.AddRange(swimlanes.OrderBy(s => s.Position));

        _currentWorkItems.Clear();
        foreach (var swimlane in _currentSwimlanes)
        {
            var items = await ApiClient.ListWorkItemsAsync(swimlane.Id);
            _currentWorkItems[swimlane.Id] = items.OrderBy(c => c.Position).ToList();
        }

        // Also load sprints for the epic
        _sprints.Clear();
        _sprints.AddRange(await ApiClient.ListSprintsAsync(_selectedEpic.Id));
    }

    private async Task LoadFeatureKanbanDataAsync()
    {
        if (_selectedFeature is null) return;
        if (_selectedProduct is null) return;

        var swimlanes = await ApiClient.ListWorkItemSwimlanesAsync(_selectedFeature.Id);
        _currentSwimlanes.Clear();
        _currentSwimlanes.AddRange(swimlanes.OrderBy(s => s.Position));

        _currentWorkItems.Clear();
        foreach (var swimlane in _currentSwimlanes)
        {
            var items = await ApiClient.ListWorkItemsAsync(swimlane.Id);
            _currentWorkItems[swimlane.Id] = items.OrderBy(c => c.Position).ToList();
        }
    }

    private async Task RefreshCurrentKanbanAsync()
    {
        switch (_view)
        {
            case TracksView.ProductKanban:
                await LoadProductKanbanDataAsync();
                break;
            case TracksView.EpicKanban:
                await LoadEpicKanbanDataAsync();
                break;
            case TracksView.FeatureKanban:
                await LoadFeatureKanbanDataAsync();
                break;
        }
        StateHasChanged();
    }

    private async Task RefreshSprintsAsync()
    {
        if (_selectedEpic is null) return;
        var planningSprintId = _planningSprint?.Id;

        _sprints.Clear();
        _sprints.AddRange(await ApiClient.ListSprintsAsync(_selectedEpic.Id));

        if (planningSprintId.HasValue)
        {
            _planningSprint = _sprints.FirstOrDefault(s => s.Id == planningSprintId.Value);
            if (_planningSprint is null && _view == TracksView.Planning)
                _view = GetKanbanView();
        }
        StateHasChanged();
    }

    // ── Kanban Event Handlers ───────────────────────────────

    private async Task HandleWorkItemMoved(WorkItemDto item)
    {
        foreach (var (_, items) in _currentWorkItems)
            items.RemoveAll(c => c.Id == item.Id);

        if (item.SwimlaneId.HasValue && _currentWorkItems.TryGetValue(item.SwimlaneId.Value, out var target))
        {
            target.Add(item);
            target.Sort((a, b) => a.Position.CompareTo(b.Position));
        }
    }

    private async Task HandleWorkItemCreated(WorkItemDto item)
    {
        if (item.SwimlaneId.HasValue && _currentWorkItems.TryGetValue(item.SwimlaneId.Value, out var items))
        {
            items.Add(item);
            items.Sort((a, b) => a.Position.CompareTo(b.Position));
        }
    }

    private async Task HandleWorkItemUpdated(WorkItemDto item)
    {
        _selectedWorkItem = item;
        foreach (var (_, items) in _currentWorkItems)
        {
            var index = items.FindIndex(c => c.Id == item.Id);
            if (index >= 0) { items[index] = item; break; }
        }
    }

    private async Task HandleWorkItemDeleted(Guid workItemId)
    {
        foreach (var (_, items) in _currentWorkItems)
            items.RemoveAll(c => c.Id == workItemId);
        if (_selectedWorkItem?.Id == workItemId) _selectedWorkItem = null;
    }

    private async Task HandleSwimlaneCreated(SwimlaneDto swimlane)
    {
        _currentSwimlanes.Add(swimlane);
        _currentSwimlanes.Sort((a, b) => a.Position.CompareTo(b.Position));
        _currentWorkItems[swimlane.Id] = [];
    }

    private async Task HandleSwimlaneDeleted(Guid swimlaneId)
    {
        _currentSwimlanes.RemoveAll(l => l.Id == swimlaneId);
        _currentWorkItems.Remove(swimlaneId);
    }

    // ── Product Event Handlers ──────────────────────────────

    private async Task HandleProductCreated(ProductDto product)
    {
        _products.Insert(0, product);
        await SelectProduct(product.Id);
    }

    private async Task HandleProductDeleted(Guid productId)
    {
        var deletedProduct = _products.FirstOrDefault(p => p.Id == productId);
        _products.RemoveAll(p => p.Id == productId);
        if (_selectedProduct?.Id == productId)
            NavigateToProductList();

        // Show undo toast so users can recover accidentally deleted products
        if (deletedProduct is not null && _undoToast is not null)
        {
            _lastDeletedProductId = productId;
            _undoToastMessage = $"\"{deletedProduct.Name}\" moved to trash.";
            await _undoToast.ShowAsync();
        }
    }

    /// <summary>
    /// Restores the most recently deleted product. Called from the Undo toast.
    /// </summary>
    private async Task RestoreLastDeletedProductAsync()
    {
        if (!_lastDeletedProductId.HasValue) return;

        try
        {
            var restored = await ApiClient.RestoreProductAsync(_lastDeletedProductId.Value);
            if (restored is not null)
            {
                _products.Insert(0, restored);
                StateHasChanged();
            }
        }
        catch
        {
            // Restore failed silently
        }
        finally
        {
            _lastDeletedProductId = null;
        }
    }

    private async Task HandleProductRestored(ProductDto product)
    {
        _products.Insert(0, product);
    }

    // ── Drilling / Navigation ───────────────────────────────

    private void NavigateToProductList()
    {
        _view = TracksView.ProductList;
        _selectedProduct = null;
        _selectedEpic = null;
        _selectedFeature = null;
        _selectedWorkItem = null;
    }

    private TracksView GetKanbanView() => _selectedFeature is not null ? TracksView.FeatureKanban
        : _selectedEpic is not null ? TracksView.EpicKanban
        : TracksView.ProductKanban;

    // ── Epic Sub-views ──────────────────────────────────────

    private void ToggleSprints() => _showSprints = !_showSprints;

    private void OpenSprintPlanning(SprintDto sprint)
    {
        _planningSprint = sprint;
        _selectedWorkItem = null;
        _showSprints = false;
        _view = TracksView.Planning;
    }

    private void ClosePlanning()
    {
        _planningSprint = null;
        _view = GetKanbanView();
    }

    private void OpenBacklog()
    {
        _selectedWorkItem = null;
        _showSprints = false;
        _view = TracksView.Backlog;
    }

    private void OpenTimeline()
    {
        _selectedWorkItem = null;
        _showSprints = false;
        _view = TracksView.Timeline;
    }

    private void OpenWizard()
    {
        _selectedWorkItem = null;
        _showSprints = false;
        _view = TracksView.Wizard;
    }

    private async Task OpenSettings()
    {
        _selectedWorkItem = null;
        _showSprints = false;
        
        if (_selectedProduct is not null && _currentSwimlanes.Count == 0)
            await LoadProductKanbanDataAsync();
        
        _view = TracksView.Settings;
    }

    private async Task OpenCalendar()
    {
        _selectedWorkItem = null;
        _showSprints = false;
        
        if (_selectedProduct is not null && _currentSwimlanes.Count == 0)
            await LoadProductKanbanDataAsync();
        
        _view = TracksView.Calendar;
    }

    private async Task OpenReview()
    {
        if (_selectedEpic is null) return;
        _selectedWorkItem = null;
        _showSprints = false;

        _isStartingReview = true;
        _reviewStartError = null;
        try
        {
            _activeReviewSession = await ApiClient.StartReviewSessionAsync(_selectedEpic.Id);
            if (_activeReviewSession is not null)
            {
                var currentUserId = await GetCurrentUserIdAsync();
                _isHost = currentUserId.HasValue && _activeReviewSession.HostUserId == currentUserId.Value;
            }
        }
        catch (Exception ex)
        {
            _reviewStartError = ex.Message;
        }
        finally
        {
            _isStartingReview = false;
        }
        _view = TracksView.Review;
    }

    private async Task HandleReviewEnded()
    {
        _activeReviewSession = null;
        _isHost = false;
        _view = GetKanbanView();
        StateHasChanged();
    }

    private async Task HandleReviewSessionUpdated(ReviewSessionDto session)
    {
        _activeReviewSession = session;
        StateHasChanged();
    }

    private async Task HandleTimelineSprintSelected(SprintDto sprint)
    {
        _view = GetKanbanView();
        StateHasChanged();
    }

    private async Task HandlePlanAdjusted()
    {
        await RefreshSprintsAsync();
    }

    private async Task HandleBacklogChanged()
    {
        await RefreshCurrentKanbanAsync();
    }

    private async Task HandlePlanCreated(IReadOnlyList<SprintDto> overview)
    {
        await RefreshSprintsAsync();
        _view = GetKanbanView();
    }

    // ── Teams ────────────────────────────────────────────────

    private void ShowTeams()
    {
        _view = TracksView.Teams;
        _selectedProduct = null;
        _selectedEpic = null;
        _selectedFeature = null;
        _selectedWorkItem = null;
    }

    private async Task RefreshTeamsAsync()
    {
        _teams.Clear();
        _teams.AddRange(await ApiClient.ListTeamsAsync());
        StateHasChanged();
    }

    private async Task HandleProductUpdated(ProductDto product)
    {
        _selectedProduct = product;
        var index = _products.FindIndex(p => p.Id == product.Id);
        if (index >= 0) _products[index] = product;
    }

    // ── Saved Views ─────────────────────────────────────────

    private async Task HandleSaveCurrentView()
    {
        _showSaveViewDialog = true;
        _newViewName = "";
        _newViewIsShared = false;
    }

    private async Task HandleSaveViewConfirm()
    {
        if (string.IsNullOrWhiteSpace(_newViewName) || _selectedProduct is null) return;

        try
        {
            var filterJson = JsonSerializer.Serialize(new { });
            var sortJson = JsonSerializer.Serialize(new { });
            var layout = _view switch
            {
                TracksView.ProductKanban or TracksView.EpicKanban or TracksView.FeatureKanban => "Kanban",
                TracksView.Backlog => "Backlog",
                TracksView.Timeline => "Timeline",
                TracksView.Calendar => "Calendar",
                _ => "Kanban"
            };

            await ApiClient.CreateCustomViewAsync(
                _selectedProduct.Id, _newViewName.Trim(), filterJson, sortJson, null, layout, _newViewIsShared);

            _showSaveViewDialog = false;
            _customViewsSidebar?.LoadViewsAsync();
        }
        catch
        {
            // Save failed silently
            _showSaveViewDialog = false;
        }
    }

    private async Task HandleViewSelected(CustomViewDto view)
    {
        _selectedViewId = view.Id;

        // Navigate to the appropriate view based on layout
        _view = view.Layout switch
        {
            "Kanban" => GetKanbanView(),
            "Backlog" => TracksView.Backlog,
            "Timeline" => TracksView.Timeline,
            "Calendar" => TracksView.Calendar,
            _ => GetKanbanView()
        };

        StateHasChanged();
    }

    private void HandleSaveViewCancel()
    {
        _showSaveViewDialog = false;
        _newViewName = "";
    }

    private async Task HandleSaveViewNameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await HandleSaveViewConfirm();
        else if (e.Key == "Escape") HandleSaveViewCancel();
    }

    // ── Real-time Event Subscriptions ───────────────────────

    public void Dispose()
    {
        SignalRService.WorkItemActionReceived -= OnWorkItemActionReceived;
        SignalRService.SwimlaneActionReceived -= OnSwimlaneActionReceived;
        SignalRService.CommentActionReceived -= OnCommentActionReceived;
        SignalRService.SprintActionReceived -= OnSprintActionReceived;
        SignalRService.ProductMemberActionReceived -= OnProductMemberActionReceived;
        SignalRService.ReviewSessionStateChanged -= OnReviewSessionStateChanged;
        _jsRef?.Dispose();
    }

    // ── Keyboard Shortcuts ──────────────────────────────────

    /// <summary>
    /// Called from JavaScript when a key is pressed anywhere on the page.
    /// Only handles shortcuts when the user is not typing in a text field
    /// (except for Escape and ? which always work).
    /// </summary>
    [JSInvokable]
    public async Task HandleKeyDownAsync(string key, bool ctrlKey, bool isInputFocused)
    {
        // Don't intercept when user is typing in a text field (except Escape and ?)
        if (isInputFocused && key is not "Escape" and not "?")
            return;

        switch (key)
        {
            case "?":
                _showShortcutsModal = !_showShortcutsModal;
                StateHasChanged();
                break;

            case "Escape":
                if (_showShortcutsModal)
                {
                    _showShortcutsModal = false;
                    StateHasChanged();
                }
                else if (_selectedWorkItem is not null)
                {
                    CloseWorkItemDetail();
                    StateHasChanged();
                }
                break;

            case "n":
            case "N":
                // Open create wizard for current kanban view
                if (_view is TracksView.ProductKanban or TracksView.EpicKanban or TracksView.FeatureKanban)
                {
                    // The kanban board has a "New" button — user can see the highlighted button
                    // Press N to be reminded this shortcut exists
                }
                break;

            case "/":
                // Focus the search/filter input
                await JS.InvokeVoidAsync("tracksKeyboard.focusSearch");
                break;

            case "ArrowLeft":
                // Navigate up hierarchy: Feature → Epic → Product
                if (_selectedFeature is not null && _selectedEpic is not null)
                    await OpenEpicKanban(_selectedEpic.Id);
                else if (_selectedEpic is not null)
                    await SelectProduct(_selectedProduct!.Id);
                break;

            case "ArrowRight":
                // Navigate down in hierarchy — handled contextually
                break;

            case "Enter" when ctrlKey:
                // Ctrl+Enter — submit the currently focused form if any
                await JS.InvokeVoidAsync("tracksKeyboard.submitActiveForm");
                break;
        }
    }

    private void SubscribeToRealtimeEvents()
    {
        SignalRService.WorkItemActionReceived += OnWorkItemActionReceived;
        SignalRService.SwimlaneActionReceived += OnSwimlaneActionReceived;
        SignalRService.CommentActionReceived += OnCommentActionReceived;
        SignalRService.SprintActionReceived += OnSprintActionReceived;
        SignalRService.ProductMemberActionReceived += OnProductMemberActionReceived;
        SignalRService.ReviewSessionStateChanged += OnReviewSessionStateChanged;
    }

    private async void OnWorkItemActionReceived(Guid productId, Guid workItemId, string action)
    {
        if (_selectedProduct?.Id != productId) return;
        await InvokeAsync(async () =>
        {
            await RefreshCurrentKanbanAsync();
        });
    }

    private async void OnSwimlaneActionReceived(Guid productId, Guid swimlaneId, string action)
    {
        if (_selectedProduct?.Id != productId) return;
        await InvokeAsync(async () =>
        {
            await RefreshCurrentKanbanAsync();
        });
    }

    private async void OnCommentActionReceived(Guid productId, Guid workItemId, Guid commentId, string action)
    {
        if (_selectedWorkItem?.Id != workItemId) return;
        await InvokeAsync(async () =>
        {
            _selectedWorkItem = await ApiClient.GetWorkItemAsync(workItemId);
            StateHasChanged();
        });
    }

    private async void OnSprintActionReceived(Guid epicId, Guid sprintId, string action)
    {
        if (_selectedEpic?.Id != epicId) return;
        await InvokeAsync(async () => await RefreshSprintsAsync());
    }

    private async void OnProductMemberActionReceived(Guid productId, Guid userId, string action)
    {
        if (_selectedProduct?.Id != productId) return;
        await InvokeAsync(async () =>
        {
            await RefreshCurrentKanbanAsync();
        });
    }

    private async void OnReviewSessionStateChanged(Guid sessionId, Guid epicId, string action)
    {
        if (_selectedEpic?.Id != epicId) return;
        await InvokeAsync(async () =>
        {
            if (action is "ended")
            {
                _activeReviewSession = null;
                _isHost = false;
                if (_view == TracksView.Review)
                    _view = GetKanbanView();
            }
            else
            {
                try
                {
                    _activeReviewSession = await ApiClient.GetReviewSessionAsync(sessionId);
                }
                catch
                {
                    _activeReviewSession = null;
                }
            }
            StateHasChanged();
        });
    }
}

using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Multi-step wizard for creating work items at any hierarchy level.
/// Context-aware: pre-selects the correct type based on the current kanban view.
/// Product Kanban → Epic, Epic Kanban → Feature, Feature Kanban → Item, Item/SubItem Kanban → SubItem.
/// </summary>
public partial class WorkItemCreationWizard : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<WorkItemDto> OnCreated { get; set; }

    /// <summary>Pre-selected work item type based on kanban context.</summary>
    [Parameter] public WorkItemType DefaultType { get; set; } = WorkItemType.Epic;

    /// <summary>Container ID for swimlane context (Product ID or parent WorkItem ID).</summary>
    [Parameter] public Guid ContainerId { get; set; }

    /// <summary>Container type for swimlane context.</summary>
    [Parameter] public SwimlaneContainerType ContainerType { get; set; } = SwimlaneContainerType.Product;

    /// <summary>Available swimlanes to assign the new item to.</summary>
    [Parameter] public List<SwimlaneDto> Swimlanes { get; set; } = [];

    /// <summary>Available labels for the product.</summary>
    [Parameter] public List<LabelDto> Labels { get; set; } = [];

    /// <summary>Available product members who can be assigned.</summary>
    [Parameter] public List<ProductMemberDto> Members { get; set; } = [];

    private static readonly string[] _stepLabels = ["Type & Title", "Details", "Assignments", "Review"];
    private int _currentStep;

    // Step 1: Type & Title
    private WorkItemType _selectedType;
    private string _title = "";

    // Step 2: Details
    private string _description = "";
    private Priority _priority = Priority.None;
    private int? _storyPoints;
    private DateTime? _dueDate;
    private Guid? _selectedSwimlaneId;

    // Step 3: Assignments
    private readonly HashSet<Guid> _selectedMemberIds = [];
    private readonly HashSet<Guid> _selectedLabelIds = [];

    // State
    private bool _isSubmitting;
    private string? _errorMessage;

    protected override void OnInitialized()
    {
        _selectedType = DefaultType;

        // Pre-select first swimlane
        if (Swimlanes.Count > 0)
            _selectedSwimlaneId = Swimlanes[0].Id;
    }

    private string GetTypeIcon(WorkItemType type) => type switch
    {
        WorkItemType.Epic => "🏗️",
        WorkItemType.Feature => "🎯",
        WorkItemType.Item => "📋",
        WorkItemType.SubItem => "📌",
        _ => "📋"
    };

    private string GetTypeDescription(WorkItemType type) => type switch
    {
        WorkItemType.Epic => "A large body of work spanning multiple sprints. Contains Features.",
        WorkItemType.Feature => "A shippable unit of work within an Epic. Contains Items.",
        WorkItemType.Item => "A single task or story within a Feature.",
        WorkItemType.SubItem => "A small sub-task broken down from an Item.",
        _ => ""
    };

    private bool CanCreateType(WorkItemType type) => type switch
    {
        WorkItemType.Epic => DefaultType == WorkItemType.Epic,
        WorkItemType.Feature => DefaultType is WorkItemType.Epic or WorkItemType.Feature,
        WorkItemType.Item => DefaultType is WorkItemType.Epic or WorkItemType.Feature or WorkItemType.Item,
        WorkItemType.SubItem => true, // Can always create sub-items if at item level or below
        _ => false
    };

    private string CanCreateTypeReason(WorkItemType type) => type switch
    {
        WorkItemType.Epic when DefaultType != WorkItemType.Epic => "You're below the Product level. Epics can only be created from the Product Kanban.",
        WorkItemType.Feature when DefaultType == WorkItemType.Item => "You're at the Item level. Features can only be created from an Epic or Product Kanban.",
        WorkItemType.Feature when DefaultType == WorkItemType.SubItem => "You're at the SubItem level. Features can only be created from an Epic or Product Kanban.",
        WorkItemType.Item when DefaultType == WorkItemType.SubItem => "You're at the SubItem level. Items can only be created from a Feature or higher.",
        _ => ""
    };

    private void GoToStep(int step)
    {
        if (step <= _currentStep || IsStepValid(_currentStep))
            _currentStep = step;
    }

    private bool IsStepValid(int step) => step switch
    {
        0 => !string.IsNullOrWhiteSpace(_title),
        1 => true, // Details are optional
        2 => true, // Assignments are optional
        3 => true, // Review step
        _ => false
    };

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

    private void ToggleMember(Guid userId)
    {
        if (_selectedMemberIds.Contains(userId))
            _selectedMemberIds.Remove(userId);
        else
            _selectedMemberIds.Add(userId);
    }

    private void ToggleLabel(Guid labelId)
    {
        if (_selectedLabelIds.Contains(labelId))
            _selectedLabelIds.Remove(labelId);
        else
            _selectedLabelIds.Add(labelId);
    }

    private async Task SubmitAsync()
    {
        if (!IsStepValid(0)) return;

        _isSubmitting = true;
        _errorMessage = null;

        try
        {
            var dto = BuildDto();
            var targetSwimlaneId = _selectedSwimlaneId ?? Swimlanes.FirstOrDefault()?.Id;

            WorkItemDto? item = null;

            if (_selectedType == WorkItemType.SubItem && ContainerType == SwimlaneContainerType.WorkItem)
            {
                // SubItem is created under a parent work item, not a swimlane
                item = await ApiClient.CreateSubItemAsync(ContainerId, dto);
            }
            else if (targetSwimlaneId.HasValue)
            {
                item = _selectedType switch
                {
                    WorkItemType.Epic => await ApiClient.CreateEpicAsync(targetSwimlaneId.Value, dto),
                    WorkItemType.Feature => await ApiClient.CreateFeatureAsync(targetSwimlaneId.Value, dto),
                    WorkItemType.Item => await ApiClient.CreateItemAsync(targetSwimlaneId.Value, dto),
                    WorkItemType.SubItem => await ApiClient.CreateSubItemAsync(ContainerId, dto),
                    _ => null
                };
            }

            if (item is not null)
            {
                await HandleCreated(item);
            }
            else
            {
                _errorMessage = "Failed to create work item. Please try again.";
                _isSubmitting = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            _isSubmitting = false;
        }
    }

    /// <summary>Gets the DTO for submission.</summary>
    public CreateWorkItemDto BuildDto() => new()
    {
        Title = _title.Trim(),
        Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
        Priority = _priority,
        StoryPoints = _storyPoints,
        DueDate = _dueDate,
        AssigneeIds = _selectedMemberIds.ToList(),
        LabelIds = _selectedLabelIds.ToList()
    };

    public WorkItemType SelectedType => _selectedType;
    public Guid? SelectedSwimlaneId => _selectedSwimlaneId;

    public bool CanSubmit => IsStepValid(0) && !_isSubmitting;

    private async Task HandleOverlayClick() => await OnClose.InvokeAsync();
    private async Task HandleCancel() => await OnClose.InvokeAsync();
    private async Task HandleCreated(WorkItemDto item) => await OnCreated.InvokeAsync(item);
}

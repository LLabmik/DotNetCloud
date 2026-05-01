using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Code-behind for the Tracks onboarding tour.
/// Manages a 10-step guided tour with overlay highlighting, tooltip positioning,
/// localStorage persistence, and auto-scroll to target elements.
/// </summary>
public class OnboardingTourBase : ComponentBase, IDisposable
{
    [Inject] private IOnboardingStateService OnboardingState { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// The current user ID, used for per-user tour state persistence.
    /// Set by TracksPage before showing the tour.
    /// </summary>
    [Parameter] public string? UserId { get; set; }

    /// <summary>
    /// The tour ID — allows multiple distinct tours in the future.
    /// </summary>
    [Parameter] public string TourId { get; set; } = "tracks_v1";

    /// <summary>
    /// Fired when the tour is completed or skipped.
    /// </summary>
    [Parameter] public EventCallback OnTourFinished { get; set; }

    /// <summary>
    /// Fired when the user wants to open a work item detail panel (step 5).
    /// </summary>
    [Parameter] public EventCallback OnRequestOpenWorkItem { get; set; }

    /// <summary>
    /// Fired when the user wants to close the work item detail panel (after step 5).
    /// </summary>
    [Parameter] public EventCallback OnRequestCloseWorkItem { get; set; }

    /// <summary>
    /// Fired when a product should be selected (step 2).
    /// </summary>
    [Parameter] public EventCallback OnRequestSelectProduct { get; set; }

    protected bool _isVisible;
    protected int _currentStep;
    protected string _tooltipStyle = string.Empty;

    private DotNetObjectReference<OnboardingTourBase>? _jsRef;

    /// <summary>
    /// Tour step definition — each step highlights a specific UI element.
    /// </summary>
    protected sealed record TourStep(
        string Title,
        string Description,
        string TargetSelector,
        TourTooltipPosition Position,
        bool IsCenterStage
    );

    /// <summary>
    /// The 10-step tour covering all Tracks features.
    /// </summary>
    protected static readonly List<TourStep> _steps =
    [
        // Step 1 — Welcome (center stage, no element highlight)
        new(
            "Welcome to DotNetCloud Tracks!",
            "Let's take a quick tour to get you familiar with everything. It only takes about 3 minutes. Ready?",
            "", // No target — center stage
            TourTooltipPosition.Center,
            IsCenterStage: true
        ),

        // Step 2 — Products sidebar
        new(
            "Products",
            "Products are your project containers. Each product has its own boards, sprints, and settings. Click a product to dive in.",
            ".tracks-sidebar-section",
            TourTooltipPosition.Right,
            IsCenterStage: false
        ),

        // Step 3 — Kanban Board
        new(
            "Kanban Board",
            "This is your Kanban board. Swimlanes represent workflow stages. Drag cards between columns to update their status. Create your first work item to see it appear here.",
            ".tracks-board-area",
            TourTooltipPosition.Bottom,
            IsCenterStage: false
        ),

        // Step 4 — Create button
        new(
            "Creating Work Items",
            "Look for the + button in the toolbar to create work items. Tracks supports Epics (big goals), Features, Items, and Sub-Items — a full hierarchy for organizing your work.",
            ".kanban-toolbar-create",
            TourTooltipPosition.Bottom,
            IsCenterStage: false
        ),

        // Step 5 — Work Item Detail Panel (requires an open item)
        new(
            "Work Item Details",
            "The detail panel shows everything about a work item: description, comments, attachments, assignments, labels, custom fields, watchers, and dependencies. Open any card on the board to explore.",
            ".work-item-detail-panel",
            TourTooltipPosition.Left,
            IsCenterStage: false
        ),

        // Step 6 — View switcher
        new(
            "Views",
            "Switch between Kanban, List, Calendar, Dashboard, Roadmap, and Settings. Each view gives a different perspective on your work. Find them in the sidebar when viewing a product.",
            ".tracks-sidebar-section",
            TourTooltipPosition.Right,
            IsCenterStage: false
        ),

        // Step 7 — Sprints
        new(
            "Sprints",
            "Sprints are time-boxed iterations. Plan sprints from the backlog, track progress with burndown charts, and review velocity over time. Open a product and drill into an epic to access sprints.",
            ".tracks-sidebar-section",
            TourTooltipPosition.Right,
            IsCenterStage: false
        ),

        // Step 8 — Filters & Search
        new(
            "Filters & Search",
            "Filter by text, priority, label, or sprint. Save your filters as Custom Views. Press Ctrl+K anytime to open the command palette for lightning-fast navigation.",
            ".kanban-toolbar",
            TourTooltipPosition.Bottom,
            IsCenterStage: false
        ),

        // Step 9 — Product Settings
        new(
            "Product Settings",
            "Product Settings is where you configure swimlanes, labels, members, custom fields, automation rules, webhooks, templates, and more. Click the ⚙️ gear icon to explore.",
            ".tracks-sidebar-section",
            TourTooltipPosition.Right,
            IsCenterStage: false
        ),

        // Step 10 — Done (center stage)
        new(
            "You're All Set! 🎉",
            "Create your first work item or explore the dashboard. You can replay this tour anytime from the help menu (click ? in the top bar). Happy tracking!",
            "",
            TourTooltipPosition.Center,
            IsCenterStage: true
        ),
    ];

    protected TourStep _currentStepDef => _steps.Count > 0 && _currentStep < _steps.Count
        ? _steps[_currentStep]
        : _steps[0];

    /// <summary>
    /// Initializes the tour. Call from TracksPage after user ID is available.
    /// Checks localStorage for existing progress and auto-starts if not completed.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId)) return;

        _jsRef = DotNetObjectReference.Create(this);

        var completed = await OnboardingState.IsCompletedAsync(UserId, TourId);
        if (completed) return;

        var savedStep = await OnboardingState.GetCurrentStepAsync(UserId, TourId);
        _currentStep = Math.Clamp(savedStep, 0, _steps.Count - 1);

        _isVisible = true;
        await ApplyTooltipPositionAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Shows the tour programmatically (used for "Restart tour" from help menu).
    /// </summary>
    public async Task ShowAsync()
    {
        _currentStep = 0;
        _isVisible = true;
        await OnboardingState.ResetAsync(UserId ?? "", TourId);
        await ApplyTooltipPositionAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Advances to the next step, or completes the tour on the last step.
    /// </summary>
    protected async Task GoToNextAsync()
    {
        // Step-specific pre-actions (e.g., open detail panel for step 5)
        await HandleStepEntryAsync(_currentStep, isLeaving: true);

        if (_currentStep >= _steps.Count - 1)
        {
            // Tour complete
            await CompleteTourAsync();
            return;
        }

        _currentStep++;
        await PersistProgressAsync();

        // Step-specific actions
        await HandleStepEntryAsync(_currentStep, isLeaving: false);

        await ApplyTooltipPositionAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Goes back to the previous step.
    /// </summary>
    protected async Task GoToPreviousAsync()
    {
        if (_currentStep <= 0) return;

        // Undo step-specific actions from current step
        await HandleStepExitAsync(_currentStep);

        _currentStep--;
        await PersistProgressAsync();
        await ApplyTooltipPositionAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Skips the entire tour and marks it as completed.
    /// </summary>
    protected async Task SkipTourAsync()
    {
        await CompleteTourAsync();
    }

    /// <summary>
    /// Completes the tour — hides the overlay and persists completion.
    /// </summary>
    private async Task CompleteTourAsync()
    {
        _isVisible = false;
        if (!string.IsNullOrWhiteSpace(UserId))
        {
            await OnboardingState.MarkCompletedAsync(UserId, TourId);
        }

        // Clean up any step-specific state
        await HandleStepExitAsync(_currentStep);

        await OnTourFinished.InvokeAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Handles step-specific pre-entry actions when navigating steps.
    /// </summary>
    private async Task HandleStepEntryAsync(int step, bool isLeaving)
    {
        if (isLeaving)
        {
            // Actions when leaving a step
            if (step == 4) // Leaving step 5 (Work Item Details)
            {
                await OnRequestCloseWorkItem.InvokeAsync();
            }
        }
        else
        {
            // Actions when entering a step
            if (step == 1) // Step 2 — Products — ensure a product is selected
            {
                // The user may need to select a product; fire the callback
            }
            else if (step == 4) // Step 5 — Work Item Details — open detail panel
            {
                await OnRequestOpenWorkItem.InvokeAsync();
            }
        }
    }

    /// <summary>
    /// Handles cleanup when leaving a step (for back-navigation).
    /// </summary>
    private async Task HandleStepExitAsync(int step)
    {
        if (step == 4) // Leaving step 5
        {
            await OnRequestCloseWorkItem.InvokeAsync();
        }
    }

    /// <summary>
    /// Positions the tooltip near the target element using JavaScript interop.
    /// Falls back to center position if the element is not found.
    /// </summary>
    private async Task ApplyTooltipPositionAsync()
    {
        if (_currentStepDef.IsCenterStage)
        {
            _tooltipStyle = "top: 50%; left: 50%; transform: translate(-50%, -50%);";
        }
        else if (!string.IsNullOrWhiteSpace(_currentStepDef.TargetSelector))
        {
            try
            {
                // Use JS to calculate position relative to the target element
                var result = await JS.InvokeAsync<string>(
                    "tracksTour.positionTooltip",
                    _currentStepDef.TargetSelector,
                    _currentStepDef.Position.ToString().ToLowerInvariant());

                if (!string.IsNullOrWhiteSpace(result))
                {
                    _tooltipStyle = result;
                }
                else
                {
                    // Fallback to center
                    _tooltipStyle = "top: 50%; left: 50%; transform: translate(-50%, -50%);";
                }
            }
            catch
            {
                // JS interop failed — fallback to center
                _tooltipStyle = "top: 50%; left: 50%; transform: translate(-50%, -50%);";
            }
        }

        // Highlight the target element
        await HighlightTargetAsync();
    }

    /// <summary>
    /// Adds/removes highlight classes on target elements via JS.
    /// Center-stage steps clear any existing highlight.
    /// </summary>
    private async Task HighlightTargetAsync()
    {
        try
        {
            // Clear previous highlights
            await JS.InvokeVoidAsync("tracksTour.clearHighlights");

            if (!_currentStepDef.IsCenterStage && !string.IsNullOrWhiteSpace(_currentStepDef.TargetSelector))
            {
                await JS.InvokeVoidAsync("tracksTour.highlightElement", _currentStepDef.TargetSelector);
            }
        }
        catch
        {
            // JS interop may fail in some environments
        }
    }

    /// <summary>
    /// Persists the current step to localStorage.
    /// </summary>
    private async Task PersistProgressAsync()
    {
        if (!string.IsNullOrWhiteSpace(UserId))
        {
            await OnboardingState.SetStepAsync(UserId, TourId, _currentStep);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _jsRef?.Dispose();
    }
}

/// <summary>
/// Positioning mode for tour tooltips relative to the target element.
/// </summary>
public enum TourTooltipPosition
{
    Top,
    Bottom,
    Left,
    Right,
    Center,
}

using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Code-behind for the Tracks onboarding tour.
/// A simple center-stage informational walkthrough that doesn't depend on DOM elements.
/// All steps display as centered tooltips with a dimmed overlay.
/// </summary>
public class OnboardingTourBase : ComponentBase, IDisposable
{
    [Inject] private IOnboardingStateService OnboardingState { get; set; } = default!;

    [Parameter] public string? UserId { get; set; }
    [Parameter] public string TourId { get; set; } = "tracks_v1";
    [Parameter] public EventCallback OnTourFinished { get; set; }

    protected bool _isVisible;
    protected int _currentStep;

    protected sealed record TourStep(string Emoji, string Title, string Description);

    protected static readonly List<TourStep> _steps =
    [
        new("👋", "Welcome to Tracks",
            "Tracks is your project management toolkit — kanban boards, sprints, backlogs, and more. Let's take a quick tour!"),

        new("📦", "Products",
            "Everything in Tracks lives inside a Product. Create one for each project you manage. Each product gets its own boards, labels, members, and settings."),

        new("📋", "Kanban Boards",
            "Inside each product, you'll find a Kanban board with swimlanes (columns) like Backlog, To Do, In Progress, and Done. Drag work items between columns as they progress."),

        new("📝", "Work Items",
            "Work items are your tasks, features, and bugs. Tracks supports a full hierarchy: Epics → Features → Items → Sub-Items. Each item has descriptions, comments, attachments, labels, and assignments."),

        new("👀", "Views",
            "Tracks offers multiple views: Kanban, List, Calendar, Dashboard, and Roadmap. Switch between them in the sidebar when you're inside a product."),

        new("🏃", "Sprints",
            "Sprints are time-boxed work periods. Plan your sprint from the backlog, track progress with burndown charts, and review velocity to improve over time."),

        new("🔍", "Filter & Search",
            "Filter items by text, priority, label, or sprint. Save your filters as Custom Views. Press Ctrl+K to open the command palette for fast keyboard navigation."),

        new("⚙️", "Settings & Power Tools",
            "Product Settings let you configure swimlanes, labels, members, custom fields, automation rules, and webhooks. Tracks grows with your team."),

        new("🚀", "You're All Set!",
            "Now go create your first product and start organizing! You can replay this tour anytime from the Show Tour button in the sidebar. Happy tracking!"),
    ];

    protected TourStep _currentStepDef => _steps.Count > 0 && _currentStep < _steps.Count
        ? _steps[_currentStep]
        : _steps[0];

    public async Task InitializeAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        UserId = userId;

        var completed = await OnboardingState.IsCompletedAsync(userId, TourId);
        if (completed) return;

        var savedStep = await OnboardingState.GetCurrentStepAsync(userId, TourId);
        _currentStep = Math.Clamp(savedStep, 0, _steps.Count - 1);

        _isVisible = true;
        StateHasChanged();
    }

    public async Task ShowAsync()
    {
        _currentStep = 0;
        _isVisible = true;
        if (!string.IsNullOrWhiteSpace(UserId))
            await OnboardingState.ResetAsync(UserId, TourId);
        StateHasChanged();
    }

    protected async Task GoToNextAsync()
    {
        if (_currentStep >= _steps.Count - 1)
        {
            await CompleteTourAsync();
            return;
        }
        _currentStep++;
        await PersistProgressAsync();
        StateHasChanged();
    }

    protected async Task GoToPreviousAsync()
    {
        if (_currentStep <= 0) return;
        _currentStep--;
        await PersistProgressAsync();
        StateHasChanged();
    }

    protected async Task SkipTourAsync()
    {
        await CompleteTourAsync();
    }

    private async Task CompleteTourAsync()
    {
        _isVisible = false;
        if (!string.IsNullOrWhiteSpace(UserId))
            await OnboardingState.MarkCompletedAsync(UserId, TourId);
        await OnTourFinished.InvokeAsync();
        StateHasChanged();
    }

    private async Task PersistProgressAsync()
    {
        if (!string.IsNullOrWhiteSpace(UserId))
            await OnboardingState.SetStepAsync(UserId, TourId, _currentStep);
    }

    public void Dispose() { }
}

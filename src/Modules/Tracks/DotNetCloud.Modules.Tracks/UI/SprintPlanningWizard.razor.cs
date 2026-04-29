using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Multi-step wizard for creating a year sprint plan on an Epic.
/// Steps: Plan Basics → Swimlane Definition → Sprint Schedule → Review &amp; Create.
/// </summary>
public partial class SprintPlanningWizard : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    /// <summary>The epic to create the sprint plan for.</summary>
    [Parameter, EditorRequired] public WorkItemDto Epic { get; set; } = default!;

    /// <summary>Existing swimlanes on the epic (empty if new epic).</summary>
    [Parameter] public IReadOnlyList<SwimlaneDto> ExistingSwimlanes { get; set; } = [];

    /// <summary>Called when the wizard completes successfully.</summary>
    [Parameter] public EventCallback<IReadOnlyList<SprintDto>> OnPlanCreated { get; set; }

    /// <summary>Called when the user cancels the wizard.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    private readonly string[] _steps = ["Plan Basics", "Swimlanes", "Sprint Schedule", "Review & Create"];
    private int _currentStep;

    // Step 1: Plan Basics
    private DateTime _startDate = DateTime.UtcNow.Date.AddDays(1 - (int)DateTime.UtcNow.DayOfWeek + 7); // Next Monday
    private int _sprintCount = 12;
    private int _defaultDurationWeeks = 2;

    // Step 2: Swimlanes
    private readonly List<SwimlaneEntry> _swimlanes = [];

    // Step 3: Sprint Schedule (generated from step 1)
    private readonly List<SprintScheduleEntry> _sprintSchedule = [];

    // State
    private bool _isCreating;
    private string? _errorMessage;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_swimlanes.Count == 0)
        {
            if (ExistingSwimlanes.Count > 0)
            {
                _swimlanes.AddRange(ExistingSwimlanes.Select(s => new SwimlaneEntry
                {
                    Title = s.Title,
                    IsDone = s.IsDone
                }));
            }
            else
            {
                // Default swimlanes for a new epic
                _swimlanes.AddRange([
                    new SwimlaneEntry { Title = "To Do" },
                    new SwimlaneEntry { Title = "In Progress" },
                    new SwimlaneEntry { Title = "Review" },
                    new SwimlaneEntry { Title = "Done", IsDone = true }
                ]);
            }
        }
    }

    // ── Navigation ──────────────────────────────────────────

    private bool CanAdvance => _currentStep switch
    {
        0 => _sprintCount is >= 1 and <= 104 && _defaultDurationWeeks is >= 1 and <= 16,
        1 => _swimlanes.Count > 0 && _swimlanes.All(s => !string.IsNullOrWhiteSpace(s.Title)),
        2 => _sprintSchedule.Count > 0,
        _ => false
    };

    private void NextStep()
    {
        if (!CanAdvance) return;
        _errorMessage = null;

        if (_currentStep == 1)
        {
            // Entering step 3: generate sprint schedule from step 1 settings
            RegenerateSchedule();
        }

        _currentStep++;
    }

    private void PreviousStep()
    {
        if (_currentStep > 0)
        {
            _errorMessage = null;
            _currentStep--;
        }
    }

    private void GoToStep(int step)
    {
        if (step < _currentStep)
        {
            _errorMessage = null;
            _currentStep = step;
        }
    }

    private async Task HandleCancel()
    {
        await OnCancel.InvokeAsync();
    }

    // ── Step 1: Plan Basics ─────────────────────────────────

    private void OnStartDateChanged(ChangeEventArgs e)
    {
        if (DateTime.TryParse(e.Value?.ToString(), out var date))
        {
            _startDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }
    }

    // ── Step 2: Swimlanes ───────────────────────────────────

    private void AddSwimlane()
    {
        _swimlanes.Add(new SwimlaneEntry { Title = "" });
    }

    private void RemoveSwimlane(int index)
    {
        if (index >= 0 && index < _swimlanes.Count && _swimlanes.Count > 1)
        {
            _swimlanes.RemoveAt(index);
        }
    }

    private void UpdateSwimlaneName(int index, string name)
    {
        if (index >= 0 && index < _swimlanes.Count)
        {
            _swimlanes[index].Title = name;
        }
    }

    private void ToggleSwimlaneIsDone(int index)
    {
        if (index >= 0 && index < _swimlanes.Count)
        {
            _swimlanes[index].IsDone = !_swimlanes[index].IsDone;
        }
    }

    // ── Step 3: Sprint Schedule ─────────────────────────────

    private void RegenerateSchedule()
    {
        _sprintSchedule.Clear();
        var currentStart = _startDate;

        for (var i = 0; i < _sprintCount; i++)
        {
            var end = currentStart.AddDays(_defaultDurationWeeks * 7);
            _sprintSchedule.Add(new SprintScheduleEntry
            {
                Order = i + 1,
                Start = currentStart,
                End = end,
                DurationWeeks = _defaultDurationWeeks
            });
            currentStart = end;
        }
    }

    private void AdjustSprintDuration(int index, int newDurationWeeks)
    {
        if (index < 0 || index >= _sprintSchedule.Count) return;
        if (newDurationWeeks < 1 || newDurationWeeks > 16) return;

        _sprintSchedule[index].DurationWeeks = newDurationWeeks;
        _sprintSchedule[index].End = _sprintSchedule[index].Start.AddDays(newDurationWeeks * 7);

        // Cascade dates to subsequent sprints
        for (var i = index + 1; i < _sprintSchedule.Count; i++)
        {
            _sprintSchedule[i].Start = _sprintSchedule[i - 1].End;
            _sprintSchedule[i].End = _sprintSchedule[i].Start.AddDays(_sprintSchedule[i].DurationWeeks * 7);
        }
    }

    // ── Step 4: Create ──────────────────────────────────────

    private async Task CreatePlanAsync()
    {
        if (_isCreating) return;
        _isCreating = true;
        _errorMessage = null;

        try
        {
            // 1. Create swimlanes if the epic doesn't have them yet
            if (ExistingSwimlanes.Count == 0)
            {
                for (var i = 0; i < _swimlanes.Count; i++)
                {
                    await ApiClient.CreateWorkItemSwimlaneAsync(Epic.Id, new CreateSwimlaneDto
                    {
                        Title = _swimlanes[i].Title,
                        IsDone = _swimlanes[i].IsDone
                    });
                }
            }

            // 2. Create the sprint plan
            var dto = new CreateSprintPlanDto
            {
                StartDate = _startDate,
                NumberOfSprints = _sprintSchedule.Count,
                SprintDurationWeeks = _defaultDurationWeeks
            };

            var overview = await ApiClient.CreateSprintPlanAsync(Epic.Id, dto);

            // 3. Adjust individual sprint durations that differ from the default
            if (overview is not null)
            {
                for (var i = 0; i < _sprintSchedule.Count && i < overview.Count; i++)
                {
                    if (_sprintSchedule[i].DurationWeeks != _defaultDurationWeeks)
                    {
                        var sprintId = overview[i].Id;
                        var adjusted = await ApiClient.AdjustSprintDatesAsync(sprintId, new AdjustSprintDto
                        {
                            DurationWeeks = _sprintSchedule[i].DurationWeeks
                        });
                        if (adjusted is not null) overview = adjusted;
                    }
                }

                await OnPlanCreated.InvokeAsync(overview);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isCreating = false;
        }
    }

    // ── Models ──────────────────────────────────────────────

    /// <summary>Local model for swimlane definition in the wizard.</summary>
    internal sealed class SwimlaneEntry
    {
        /// <summary>Swimlane title.</summary>
        public string Title { get; set; } = "";

        /// <summary>Whether this swimlane is a "done" column.</summary>
        public bool IsDone { get; set; }
    }

    /// <summary>Local model for sprint schedule preview.</summary>
    internal sealed class SprintScheduleEntry
    {
        /// <summary>Sprint order (1-based).</summary>
        public int Order { get; set; }

        /// <summary>Sprint start date.</summary>
        public DateTime Start { get; set; }

        /// <summary>Sprint end date.</summary>
        public DateTime End { get; set; }

        /// <summary>Duration in weeks.</summary>
        public int DurationWeeks { get; set; }
    }
}

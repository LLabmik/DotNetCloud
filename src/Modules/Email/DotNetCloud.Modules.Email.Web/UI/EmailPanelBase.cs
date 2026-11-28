using DotNetCloud.Modules.Email.Web.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Email.Web.UI;

/// <summary>
/// Base for the Email panels: subscribes to <see cref="EmailState"/> so every view follows one state.
/// </summary>
public abstract class EmailPanelBase : ComponentBase, IDisposable
{
    /// <summary>Gets the shared UI state.</summary>
    [Inject]
    protected EmailState State { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized() => State.Changed += OnStateChanged;

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        GC.SuppressFinalize(this);
    }
}

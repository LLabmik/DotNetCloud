using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Calendar.UI;

/// <summary>Code-behind for the <see cref="ColorSwatchPicker"/> component.</summary>
public partial class ColorSwatchPicker : ComponentBase
{
    private static readonly string[] DefaultSwatches = ["#ef4444", "#f97316", "#eab308", "#22c55e", "#3b82f6", "#8b5cf6", "#ec4899", "#6b7280"];

    [Parameter] public string? Color { get; set; }
    [Parameter] public EventCallback<string?> ColorChanged { get; set; }
    [Parameter] public IReadOnlyList<string> Swatches { get; set; } = DefaultSwatches;

    private async Task ToggleSwatch(string swatch)
    {
        var next = Color == swatch ? null : swatch;
        Color = next;
        await ColorChanged.InvokeAsync(next);
    }

    private async Task OnCustomColorChanged(ChangeEventArgs e)
    {
        var next = e.Value?.ToString();
        Color = next;
        await ColorChanged.InvokeAsync(next);
    }
}

using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Forms;

/// <summary>Code-behind for the <see cref="DncDatePicker"/> component.</summary>
public partial class DncDatePicker
{
    private string _id = $"dnc-datepicker-{Guid.NewGuid():N}";

    /// <summary>Label text displayed above the date picker.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Whether the date picker is disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Input type: "date", "datetime-local", "month", or "time".</summary>
    [Parameter]
    public new string Type { get; set; } = "date";

    /// <summary>Expression for validation message binding.</summary>
    [Parameter]
    public Expression<Func<DateTime?>>? ValidationFor { get; set; }

    private void OnDateChanged(ChangeEventArgs e)
    {
        CurrentValueAsString = e.Value?.ToString();
    }
}

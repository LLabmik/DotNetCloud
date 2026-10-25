using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Forms;

/// <summary>Code-behind for the <see cref="DncSelect"/> component.</summary>
public partial class DncSelect
{
    private string _id = $"dnc-select-{Guid.NewGuid():N}";

    /// <summary>Label text displayed above the select.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Text for the default empty option (e.g. "-- Select --").</summary>
    [Parameter]
    public string? DefaultText { get; set; }

    /// <summary>Whether the select is disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Expression for validation message binding.</summary>
    [Parameter]
    public Expression<Func<string>>? ValidationFor { get; set; }
}

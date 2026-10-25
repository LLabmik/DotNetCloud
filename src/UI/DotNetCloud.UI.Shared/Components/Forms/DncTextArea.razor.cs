using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Forms;

/// <summary>Code-behind for the <see cref="DncTextArea"/> component.</summary>
public partial class DncTextArea
{
    private string _id = $"dnc-textarea-{Guid.NewGuid():N}";

    /// <summary>Label text displayed above the textarea.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Number of visible text rows.</summary>
    [Parameter]
    public int Rows { get; set; } = 3;

    /// <summary>Placeholder text.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>Whether the textarea is disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Expression for validation message binding.</summary>
    [Parameter]
    public Expression<Func<string>>? ValidationFor { get; set; }
}

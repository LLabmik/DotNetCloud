using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Forms;

/// <summary>Code-behind for the <see cref="DncRadioGroup"/> component.</summary>
public partial class DncRadioGroup
{
    private string _groupId = $"dnc-radio-{Guid.NewGuid():N}";

    /// <summary>Label text displayed above the radio group.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Whether to display options inline.</summary>
    [Parameter]
    public bool Inline { get; set; }

    /// <summary>Whether the radio buttons are disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>The available radio options.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<RadioOption> Options { get; set; } = [];

    /// <summary>Expression for validation message binding.</summary>
    [Parameter]
    public Expression<Func<string>>? ValidationFor { get; set; }

    protected override bool TryParseValueFromString(string? value, out string result, out string validationErrorMessage)
    {
        result = value ?? string.Empty;
        validationErrorMessage = string.Empty;
        return true;
    }
}

using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Forms;

/// <summary>Code-behind for the <see cref="DncCheckbox"/> component.</summary>
public partial class DncCheckbox
{
    /// <summary>Label text displayed next to the checkbox.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Whether the checkbox is disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Expression for validation message binding.</summary>
    [Parameter]
    public Expression<Func<bool>>? ValidationFor { get; set; }

    private void OnCheckedChanged(ChangeEventArgs e)
    {
        CurrentValue = (bool)(e.Value ?? false);
    }

    protected override bool TryParseValueFromString(string? value, out bool result, out string validationErrorMessage)
    {
        result = bool.TryParse(value, out var parsed) && parsed;
        validationErrorMessage = string.Empty;
        return true;
    }
}

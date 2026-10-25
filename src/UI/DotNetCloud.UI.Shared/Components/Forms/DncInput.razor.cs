using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Forms;

/// <summary>Code-behind for the <see cref="DncInput"/> component.</summary>
public partial class DncInput
{
    private string _id = $"dnc-input-{Guid.NewGuid():N}";

    /// <summary>Label text displayed above the input.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>HTML input type (text, password, email, number, etc.).</summary>
    [Parameter]
    public string InputType { get; set; } = "text";

    /// <summary>Placeholder text.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>Whether the input is disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

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

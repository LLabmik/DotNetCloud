using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>
/// Renders a UTC timestamp as a relative age with an ISO-8601 tooltip.
/// Using this component keeps the tooltip and the formatted text in sync across
/// admin tables and list rows.
/// </summary>
public partial class RelativeTimestamp : ComponentBase
{
    /// <summary>
    /// The value to render. Takes precedence over <see cref="Utc"/>.
    /// </summary>
    [Parameter]
    public DateTimeOffset? Value { get; set; }

    /// <summary>
    /// A UTC <see cref="DateTime"/> to render. Use this when the source type is
    /// <see cref="DateTime"/> instead of <see cref="DateTimeOffset"/>.
    /// </summary>
    [Parameter]
    public DateTime? Utc { get; set; }

    /// <summary>Relative-time shaping options. Defaults to the compact style.</summary>
    [Parameter]
    public RelativeTimeOptions Options { get; set; } = RelativeTimeOptions.Default;

    /// <summary>Optional CSS class applied to the wrapping <c>span</c>.</summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>Whether to render the ISO-8601 tooltip. Defaults to <see langword="true"/>.</summary>
    [Parameter]
    public bool ShowTitle { get; set; } = true;

    /// <summary>
    /// Text shown when the value is null. Defaults to the formatter's em dash; use this
    /// to preserve caller-specific wording such as "Never".
    /// </summary>
    [Parameter]
    public string? EmptyText { get; set; }

    private DateTimeOffset? EffectiveValue
        => Value ?? (Utc is { } utc ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) : null);

    private string Title
        => ShowTitle && EffectiveValue is { } value ? value.ToString("O") : string.Empty;

    private string DisplayText
        => EmptyText is null
            ? RelativeTimeFormatter.Format(EffectiveValue, Options)
            : RelativeTimeFormatter.Format(EffectiveValue, Options with { EmptyText = EmptyText });
}

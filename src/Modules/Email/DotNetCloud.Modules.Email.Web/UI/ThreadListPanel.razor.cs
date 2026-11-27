using DotNetCloud.Modules.Email.Web.Services;

namespace DotNetCloud.Modules.Email.Web.UI;

/// <summary>Code-behind for the thread list.</summary>
public partial class ThreadListPanel : EmailPanelBase
{
    private IReadOnlyList<EmailThreadDto> SortedThreads =>
        [.. State.Threads.OrderByDescending(thread => thread.LastMessageAt ?? DateTime.MinValue)];

    private static string FormatListDate(DateTime? value)
    {
        if (value is not { } date || date == default)
        {
            return string.Empty;
        }

        var local = date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc).ToLocalTime()
            : date.ToLocalTime();

        return local.ToString("MMM dd", System.Globalization.CultureInfo.CurrentCulture);
    }
}

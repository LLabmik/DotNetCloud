using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the typing indicator component.
/// Shows animated dots and names of users currently typing.
/// </summary>
public partial class TypingIndicator : ComponentBase, IDisposable
{
    private Timer? _expiryTimer;

    /// <summary>Users currently typing.</summary>
    [Parameter]
    public List<TypingUserViewModel> TypingUsers { get; set; } = [];

    /// <summary>Auto-expire interval in milliseconds.</summary>
    [Parameter]
    public int ExpireMs { get; set; } = 5000;

    /// <summary>Callback to request removal of expired entries.</summary>
    [Parameter]
    public EventCallback OnExpire { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _expiryTimer = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await OnExpire.InvokeAsync();
                StateHasChanged();
            });
        }, null, ExpireMs, ExpireMs);
    }

    /// <summary>Gets the display text for typing users.</summary>
    protected string GetTypingText()
    {
        return TypingUsers.Count switch
        {
            0 => string.Empty,
            1 => $"{TypingUsers[0].DisplayName} is typing…",
            2 => $"{TypingUsers[0].DisplayName} and {TypingUsers[1].DisplayName} are typing…",
            _ => "Several people are typing…"
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _expiryTimer?.Dispose();
    }
}

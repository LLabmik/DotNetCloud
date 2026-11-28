using DotNetCloud.Modules.Email.Web.Services;
using DotNetCloud.Modules.Email.Web.UI;
using DotNetCloud.Modules.Sdk.Web;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Email.Web.Pages;

/// <summary>
/// Code-behind for the Email module UI shell: the sidebar, the panel switch and the two overlays.
/// </summary>
/// <remarks>
/// The behaviour lives in <see cref="EmailState"/>; this page only starts it, reads the one-shot query
/// parameters the Gmail callback and the Home widget append, and forwards overlay actions.
/// </remarks>
public partial class Email : EmailPanelBase
{
    /// <summary>Gets or sets the thread the shell deep-linked to.</summary>
    [SupplyParameterFromQuery(Name = "threadId")]
    public string? ThreadId { get; set; }

    /// <summary>Gets or sets the Gmail callback status.</summary>
    [SupplyParameterFromQuery(Name = "gmail")]
    public string? GmailStatus { get; set; }

    /// <summary>Gets or sets the Gmail callback failure reason.</summary>
    [SupplyParameterFromQuery(Name = "reason")]
    public string? GmailReason { get; set; }

    [Inject]
    private ModuleFrameBridge Bridge { get; set; } = default!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        base.OnInitialized();
        await State.InitializeAsync();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await Bridge.InitializeAsync();
        ApplyStatusParameters();

        if (Guid.TryParse(ThreadId, out var threadId))
        {
            await State.OpenThreadFromQueryAsync(threadId);
        }
    }

    /// <summary>
    /// Reports the Gmail callback outcome the core appended to the frame URL.
    /// </summary>
    private void ApplyStatusParameters()
    {
        switch (GmailStatus)
        {
            case "connected":
                State.ReportSuccess("Gmail account connected.");
                break;
            case "denied":
                State.ReportSuccess("Gmail authorisation was cancelled.");
                break;
            case "error":
                State.ReportError(GmailReason switch
                {
                    "not_configured" => "Gmail OAuth is not configured by the server administrator.",
                    "not_signed_in" => "Sign in to DotNetCloud, then try connecting Gmail again.",
                    _ => "Gmail authorisation failed. Try again.",
                });
                break;
        }
    }

    private async Task OnAccountChanged(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var accountId))
        {
            await State.SelectAccountAsync(accountId);
        }
    }

    private static string MailboxIcon(EmailMailboxDto mailbox)
    {
        var flags = mailbox.SyncFlags;

        return (flags & EmailState.InboxFlag) != 0 ? "move_to_inbox"
            : (flags & EmailState.SentFlag) != 0 ? "outbox"
            : (flags & EmailState.DraftsFlag) != 0 ? "draft"
            : (flags & 4) != 0 ? "delete"
            : (flags & 32) != 0 ? "block"
            : (flags & 16) != 0 ? "inventory_2"
            : "folder";
    }
}

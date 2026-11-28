using DotNetCloud.Modules.Email.Web.Services;

namespace DotNetCloud.Modules.Email.Web.UI;

/// <summary>
/// Code-behind for the add-account form.
/// </summary>
/// <remarks>
/// The credentials live in this component, not in the shared state: they must never be re-rendered
/// into another panel or left behind after the account is created.
/// </remarks>
public partial class AddAccountPanel : EmailPanelBase
{
    private AccountTab _tab = AccountTab.Imap;

    private string _displayName = string.Empty;
    private string _emailAddress = string.Empty;
    private string _imapHost = string.Empty;
    private int _imapPort = 993;
    private string _smtpHost = string.Empty;
    private int _smtpPort = 587;
    private string _username = string.Empty;
    private string _password = string.Empty;

    private void Cancel()
    {
        State.View = State.SelectedAccount is null ? EmailView.Welcome : EmailView.Mailbox;
    }

    private async Task AddImapAsync()
    {
        await State.AddImapAccountAsync(
            _displayName,
            _emailAddress,
            _imapHost,
            _imapPort,
            _smtpHost,
            _smtpPort,
            _username,
            _password);

        _password = string.Empty;
    }

    private enum AccountTab
    {
        Imap,
        Gmail,
    }
}

using System.Text.Json;
using DotNetCloud.Modules.Sdk.Web;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Email.Web.Services;

/// <summary>
/// Holds the Email module UI's state and every operation the panels invoke.
/// </summary>
/// <remarks>
/// <para>
/// The panels are thin views over this object, so the whole UI's behaviour is testable without a
/// browser: load accounts, pick a mailbox, open a thread, compose, send, and edit rules.
/// </para>
/// <para>
/// Every failure is turned into a banner message here. A refused <c>files:read</c>/<c>files:write</c>
/// consent becomes the shell's consent prompt, because a sandboxed frame cannot approve anything
/// itself.
/// </para>
/// </remarks>
public sealed class EmailState
{
    /// <summary>The numeric mailbox flag for the inbox.</summary>
    public const int InboxFlag = 1;

    /// <summary>The numeric mailbox flag for sent mail.</summary>
    public const int SentFlag = 2;

    /// <summary>The numeric mailbox flag for drafts.</summary>
    public const int DraftsFlag = 8;

    private readonly EmailApi _api;
    private readonly ModuleFrameBridge _bridge;
    private readonly NavigationManager _navigation;

    private TaskCompletionSource<FileChoice?>? browserCompletion;

    /// <summary>Initializes a new instance of the <see cref="EmailState"/> class.</summary>
    /// <param name="api">The module API client.</param>
    /// <param name="bridge">The frame bridge.</param>
    /// <param name="navigation">The frame's navigation manager, used to make URLs absolute.</param>
    public EmailState(EmailApi api, ModuleFrameBridge bridge, NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(navigation);
        _api = api;
        _bridge = bridge;
        _navigation = navigation;
    }

    /// <summary>Raised whenever the state changed and the views should re-render.</summary>
    public event Action? Changed;

    /// <summary>Gets the caller's accounts.</summary>
    public List<EmailAccountDto> Accounts { get; } = [];

    /// <summary>Gets the caller's rules.</summary>
    public List<EmailRuleDto> Rules { get; } = [];

    /// <summary>Gets the selected account's mailboxes.</summary>
    public List<EmailMailboxDto> Mailboxes { get; } = [];

    /// <summary>Gets the selected mailbox's threads.</summary>
    public List<EmailThreadDto> Threads { get; } = [];

    /// <summary>Gets the open thread's messages.</summary>
    public List<MessageEntry> Messages { get; } = [];

    /// <summary>Gets or sets the current panel.</summary>
    public EmailView View { get; set; } = EmailView.Welcome;

    /// <summary>Gets or sets the selected account.</summary>
    public Guid? SelectedAccountId { get; set; }

    /// <summary>Gets or sets the selected mailbox.</summary>
    public Guid? SelectedMailboxId { get; set; }

    /// <summary>Gets or sets the open thread.</summary>
    public EmailThreadDto? SelectedThread { get; set; }

    /// <summary>Gets or sets the in-progress compose form.</summary>
    public ComposeDraft? Compose { get; set; }

    /// <summary>Gets or sets the in-progress rule edit.</summary>
    public RuleDraft? Rule { get; set; }

    /// <summary>Gets or sets a value indicating whether the sidebar is collapsed.</summary>
    public bool SidebarCollapsed { get; set; }

    /// <summary>Gets or sets a value indicating whether an operation is running.</summary>
    public bool Busy { get; set; }

    /// <summary>Gets or sets the banner message.</summary>
    public string? BannerText { get; set; }

    /// <summary>Gets or sets a value indicating whether the banner reports success.</summary>
    public bool BannerIsSuccess { get; set; }

    /// <summary>Gets or sets a value indicating whether an administrator configured Gmail OAuth.</summary>
    public bool GmailConfigured { get; set; }

    /// <summary>Gets or sets the pending confirmation.</summary>
    public ConfirmRequest? Confirm { get; set; }

    /// <summary>Gets or sets the open file browser.</summary>
    public FileBrowserState? FileBrowser { get; set; }

    /// <summary>Gets the selected account.</summary>
    public EmailAccountDto? SelectedAccount =>
        Accounts.FirstOrDefault(account => account.Id == SelectedAccountId);

    /// <summary>Gets the selected mailbox.</summary>
    public EmailMailboxDto? SelectedMailbox =>
        Mailboxes.FirstOrDefault(mailbox => mailbox.Id == SelectedMailboxId);

    /// <summary>Loads everything the UI needs on first render.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the first view is ready.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            await LoadAccountsAsync(cancellationToken);
            await LoadRulesAsync(cancellationToken);
            View = Accounts.Count > 0 ? EmailView.Mailbox : EmailView.Welcome;
        }).ConfigureAwait(false);

        GmailConfigured = await _api.IsGmailConfiguredAsync(cancellationToken).ConfigureAwait(false);
        NotifyChanged();
    }

    /// <summary>Shows the rule list.</summary>
    /// <returns>A task that completes when the list is loaded.</returns>
    public async Task OpenRulesAsync()
    {
        await RunAsync(async () =>
        {
            await LoadRulesAsync();
            View = EmailView.Rules;
        }).ConfigureAwait(false);
    }

    /// <summary>Selects an account and loads its mailboxes.</summary>
    /// <param name="accountId">The account id.</param>
    /// <returns>A task that completes when the account's inbox is loaded.</returns>
    public async Task SelectAccountAsync(Guid accountId)
    {
        SelectedAccountId = accountId;
        SelectedThread = null;
        Messages.Clear();
        Threads.Clear();
        SelectedMailboxId = null;
        View = EmailView.Mailbox;

        await RunAsync(() => LoadMailboxesAsync(selectInbox: true)).ConfigureAwait(false);
    }

    /// <summary>Selects a mailbox and loads its threads.</summary>
    /// <param name="mailboxId">The mailbox id.</param>
    /// <returns>A task that completes when the threads are loaded.</returns>
    public async Task SelectMailboxAsync(Guid mailboxId)
    {
        SelectedMailboxId = mailboxId;
        SelectedThread = null;
        Messages.Clear();
        View = EmailView.Mailbox;

        await RunAsync(() => LoadThreadsAsync()).ConfigureAwait(false);
    }

    /// <summary>Opens a thread and loads its messages with their bodies.</summary>
    /// <param name="thread">The thread.</param>
    /// <returns>A task that completes when every body is loaded.</returns>
    public async Task OpenThreadAsync(EmailThreadDto thread)
    {
        SelectedThread = thread;
        Messages.Clear();
        View = EmailView.Thread;
        NotifyChanged();

        await RunAsync(async () =>
        {
            var messages = await _api.ListMessagesAsync(thread.Id).ConfigureAwait(false);

            foreach (var message in messages)
            {
                string? body = null;

                try
                {
                    body = await _api.GetBodyHtmlAsync(message.Id).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
                {
                    // A missing body falls back to the plain-text preview.
                }

                Messages.Add(new MessageEntry(message, body));
            }

            View = EmailView.Thread;
        }).ConfigureAwait(false);
    }

    /// <summary>Opens the thread named by a widget deep link.</summary>
    /// <param name="threadId">The thread id.</param>
    /// <returns>A task that completes when the thread is open, or the banner explains why not.</returns>
    public async Task OpenThreadFromQueryAsync(Guid threadId)
    {
        var current = Threads.FirstOrDefault(thread => thread.Id == threadId);
        if (current is not null)
        {
            await OpenThreadAsync(current).ConfigureAwait(false);
            return;
        }

        foreach (var account in Accounts)
        {
            IReadOnlyList<EmailMailboxDto> mailboxes;

            try
            {
                mailboxes = await _api.ListMailboxesAsync(account.Id).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
            {
                continue;
            }

            foreach (var mailbox in mailboxes)
            {
                IReadOnlyList<EmailThreadDto> threads;

                try
                {
                    threads = await _api.ListThreadsAsync(account.Id, mailbox.Id).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
                {
                    continue;
                }

                var match = threads.FirstOrDefault(thread => thread.Id == threadId);
                if (match is null)
                {
                    continue;
                }

                SelectedAccountId = account.Id;
                Mailboxes.Clear();
                Mailboxes.AddRange(SortMailboxes(mailboxes));
                SelectedMailboxId = mailbox.Id;
                Threads.Clear();
                Threads.AddRange(threads);
                await OpenThreadAsync(match).ConfigureAwait(false);
                return;
            }
        }

        SetBanner("That message could not be found.", isSuccess: false);
    }

    /// <summary>Starts a new message.</summary>
    /// <param name="thread">The thread being replied to or forwarded, when any.</param>
    /// <param name="subjectPrefix">The prefix to add to the subject (<c>Re: </c> or <c>Fwd: </c>).</param>
    public void BeginCompose(EmailThreadDto? thread = null, string? subjectPrefix = null)
    {
        if (SelectedAccount is not { } account)
        {
            SetBanner("Add an email account before composing.", isSuccess: false);
            View = EmailView.AddAccount;
            NotifyChanged();
            return;
        }

        var to = string.Empty;
        if (thread is not null && !string.Equals(subjectPrefix, "Fwd: ", StringComparison.Ordinal))
        {
            to = string.Join(
                ", ",
                ParseAddresses(thread.ParticipantsJson).Select(address =>
                    string.IsNullOrWhiteSpace(address.Name)
                        ? address.Email
                        : $"{address.Name} <{address.Email}>"));
        }

        Compose = new ComposeDraft
        {
            AccountId = account.Id,
            To = to,
            Subject = thread is null ? string.Empty : (subjectPrefix ?? string.Empty) + thread.Subject,
        };

        View = EmailView.Compose;
        NotifyChanged();
    }

    /// <summary>Sends the composed message.</summary>
    /// <returns>A task that completes when the host accepted the message.</returns>
    public async Task SendAsync()
    {
        if (Compose is not { } compose)
        {
            return;
        }

        var recipients = ParseRecipients(compose.To);
        if (recipients.Count == 0)
        {
            SetBanner("Add at least one recipient.", isSuccess: false);
            return;
        }

        if (string.IsNullOrWhiteSpace(compose.Subject))
        {
            SetBanner("Add a subject.", isSuccess: false);
            return;
        }

        await RunAsync(async () =>
        {
            await _api.SendAsync(
                compose.AccountId,
                new
                {
                    to = recipients,
                    cc = ParseRecipients(compose.Cc),
                    bcc = ParseRecipients(compose.Bcc),
                    subject = compose.Subject,
                    bodyPlainText = compose.Body,
                    bodyHtml = (string?)null,
                    attachments = compose.Attachments.Select(attachment => new
                    {
                        storageKey = attachment.StorageKey,
                        fileName = attachment.FileName,
                        contentType = attachment.ContentType,
                        size = attachment.Size,
                    }),
                }).ConfigureAwait(false);

            Compose = null;
            View = EmailView.Mailbox;
            SetBanner("Message sent.", isSuccess: true);
            await LoadMailboxesAsync(selectInbox: false).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>Uploads one staged attachment.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>A task that completes when the upload finished.</returns>
    public async Task UploadAttachmentAsync(Stream content, string fileName, string contentType)
    {
        if (Compose is not { } compose)
        {
            return;
        }

        compose.Uploading++;
        NotifyChanged();

        try
        {
            var attachment = await _api.UploadAttachmentAsync(content, fileName, contentType).ConfigureAwait(false);
            compose.Attachments.Add(attachment);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException or IOException)
        {
            SetBanner($"Could not attach \"{fileName}\": {DescribeFileError(ex)}", isSuccess: false);
        }
        finally
        {
            compose.Uploading--;
            NotifyChanged();
        }
    }

    /// <summary>Copies one of the user's cloud files into the attachment store.</summary>
    /// <param name="node">The file.</param>
    /// <returns>A task that completes when the file was attached.</returns>
    public async Task AttachFromFileAsync(EmailFileNodeDto node)
    {
        if (Compose is not { } compose)
        {
            return;
        }

        compose.Uploading++;
        NotifyChanged();

        try
        {
            var attachment = await _api
                .AttachFromFileAsync(node.Id, node.Name, node.MimeType, node.Size)
                .ConfigureAwait(false);
            compose.Attachments.Add(attachment);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            SetBanner(DescribeFileError(ex), isSuccess: false);
        }
        finally
        {
            compose.Uploading--;
            NotifyChanged();
        }
    }

    /// <summary>Copies a received attachment into the caller's cloud files.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns>A task that completes when the file was written.</returns>
    public async Task SaveAttachmentToFilesAsync(EmailAttachmentDto attachment)
    {
        var choice = await PickFromFilesAsync(FileBrowserMode.Folder).ConfigureAwait(false);
        if (choice is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _api.SaveAttachmentToFilesAsync(attachment.Id, choice.FolderId).ConfigureAwait(false);
            SetBanner($"Saved \"{attachment.FileName}\" to {choice.FolderName}.", isSuccess: true);
        }).ConfigureAwait(false);
    }

    /// <summary>Opens an attachment in a core-controlled tab.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns>A task that completes when the tab was requested.</returns>
    public async Task OpenAttachmentAsync(EmailAttachmentDto attachment) =>
        await _bridge.OpenExternalAsync(AbsoluteUrl(EmailApi.AttachmentDownloadUrl(attachment.Id))).ConfigureAwait(false);

    /// <summary>Starts the Gmail consent flow in a core-controlled tab.</summary>
    /// <returns>A task that completes when the tab was requested.</returns>
    public async Task StartGmailOAuthAsync() =>
        await _bridge.OpenExternalAsync(AbsoluteUrl(EmailApi.GmailStartUrl)).ConfigureAwait(false);

    /// <summary>Adds an IMAP/SMTP account.</summary>
    /// <param name="displayName">The display name.</param>
    /// <param name="emailAddress">The address.</param>
    /// <param name="imapHost">The IMAP host.</param>
    /// <param name="imapPort">The IMAP port.</param>
    /// <param name="smtpHost">The SMTP host.</param>
    /// <param name="smtpPort">The SMTP port.</param>
    /// <param name="username">The user name.</param>
    /// <param name="password">The password.</param>
    /// <returns>A task that completes when the account was created.</returns>
    public async Task AddImapAccountAsync(
        string displayName,
        string emailAddress,
        string imapHost,
        int imapPort,
        string smtpHost,
        int smtpPort,
        string username,
        string password)
    {
        await RunAsync(async () =>
        {
            await _api.CreateImapAccountAsync(
                displayName,
                emailAddress,
                new
                {
                    imapHost,
                    imapPort,
                    imapUseSsl = true,
                    smtpHost,
                    smtpPort,
                    smtpUseStartTls = smtpPort != 465,
                    username,
                    password,
                }).ConfigureAwait(false);

            await LoadAccountsAsync().ConfigureAwait(false);
            View = SelectedMailboxId is null ? EmailView.Welcome : EmailView.Mailbox;
            SetBanner("Account added. Use \"Sync now\" to fetch messages.", isSuccess: true);
        }).ConfigureAwait(false);
    }

    /// <summary>Starts a sync for the selected account.</summary>
    /// <returns>A task that completes when the host accepted the request.</returns>
    public async Task SyncAccountAsync()
    {
        if (SelectedAccount is not { } account)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _api.SyncAccountAsync(account.Id).ConfigureAwait(false);
            await LoadMailboxesAsync(selectInbox: true).ConfigureAwait(false);
            SetBanner("Sync started. New messages appear as they arrive.", isSuccess: true);
        }).ConfigureAwait(false);
    }

    /// <summary>Asks for confirmation, then deletes the selected account.</summary>
    public void RequestDeleteAccount()
    {
        if (SelectedAccount is not { } account)
        {
            return;
        }

        Confirm = new ConfirmRequest(
            "Delete account",
            $"Delete {account.EmailAddress} and every synced message? This cannot be undone.",
            "Delete account",
            Danger: true,
            async () =>
            {
                await RunAsync(async () =>
                {
                    await _api.DeleteAccountAsync(account.Id).ConfigureAwait(false);
                    SelectedThread = null;
                    Messages.Clear();
                    Threads.Clear();
                    await LoadAccountsAsync().ConfigureAwait(false);
                    View = Accounts.Count > 0 ? EmailView.Mailbox : EmailView.Welcome;
                    SetBanner("Account deleted.", isSuccess: true);
                }).ConfigureAwait(false);
            });

        NotifyChanged();
    }

    /// <summary>Accepts the pending confirmation.</summary>
    /// <returns>A task that completes when the action finished.</returns>
    public async Task AcceptConfirmAsync()
    {
        if (Confirm is not { } request)
        {
            return;
        }

        Confirm = null;
        NotifyChanged();
        await request.Accepted().ConfigureAwait(false);
    }

    /// <summary>Dismisses the banner.</summary>
    public void DismissBanner()
    {
        BannerText = null;
        NotifyChanged();
    }

    /// <summary>Toggles the sidebar.</summary>
    public void ToggleSidebar()
    {
        SidebarCollapsed = !SidebarCollapsed;
        NotifyChanged();
    }

    // ── Rules ───────────────────────────────────────────────

    /// <summary>Starts a new rule.</summary>
    public void NewRule()
    {
        Rule = new RuleDraft
        {
            AccountId = SelectedAccountId,
            Priority = Rules.Count + 1,
        };
        Rule.Groups.Add(new RuleGroupDraft { Conditions = { new RuleConditionDraft() } });
        View = EmailView.Rule;
        NotifyChanged();
    }

    /// <summary>Opens an existing rule for editing.</summary>
    /// <param name="rule">The rule.</param>
    public void EditRule(EmailRuleDto rule)
    {
        var draft = new RuleDraft
        {
            Id = rule.Id,
            Name = rule.Name,
            AccountId = rule.AccountId,
            IsEnabled = rule.IsEnabled,
            Priority = rule.Priority,
            StopProcessing = rule.StopProcessing,
        };

        foreach (var group in rule.ConditionGroups)
        {
            var groupDraft = new RuleGroupDraft { MatchMode = group.MatchMode };
            groupDraft.Conditions.AddRange(group.Conditions.Select(condition => new RuleConditionDraft
            {
                Field = condition.Field,
                Operator = condition.Operator,
                Value = condition.Value,
            }));
            draft.Groups.Add(groupDraft);
        }

        draft.Actions.AddRange(rule.Actions.Select(action => new RuleActionDraft
        {
            ActionType = action.ActionType,
            TargetValue = action.TargetValue,
        }));

        Rule = draft;
        View = EmailView.Rule;
        NotifyChanged();
    }

    /// <summary>Saves the rule being edited.</summary>
    /// <returns>A task that completes when the host accepted the rule.</returns>
    public async Task SaveRuleAsync()
    {
        if (Rule is not { } rule)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            SetBanner("Give the rule a name.", isSuccess: false);
            return;
        }

        var groups = rule.Groups.Select(group => new
        {
            matchMode = group.MatchMode,
            conditions = group.Conditions.Select(condition => new
            {
                field = condition.Field,
                @operator = condition.Operator,
                value = condition.Value,
            }),
        }).ToList();

        var actions = rule.Actions.Select(action => new
        {
            actionType = action.ActionType,
            targetValue = string.IsNullOrWhiteSpace(action.TargetValue) ? null : action.TargetValue,
        }).ToList();

        await RunAsync(async () =>
        {
            if (rule.Id is { } ruleId)
            {
                await _api.UpdateRuleAsync(ruleId, new
                {
                    name = rule.Name,
                    isEnabled = rule.IsEnabled,
                    priority = rule.Priority,
                    stopProcessing = rule.StopProcessing,
                    conditionGroups = groups,
                    actions,
                }).ConfigureAwait(false);
            }
            else
            {
                await _api.CreateRuleAsync(new
                {
                    name = rule.Name,
                    accountId = rule.AccountId,
                    isEnabled = rule.IsEnabled,
                    priority = rule.Priority,
                    stopProcessing = rule.StopProcessing,
                    conditionGroups = groups,
                    actions,
                }).ConfigureAwait(false);
            }

            await LoadRulesAsync().ConfigureAwait(false);
            Rule = null;
            View = EmailView.Rules;
            SetBanner("Rule saved.", isSuccess: true);
        }).ConfigureAwait(false);
    }

    /// <summary>Asks for confirmation, then deletes a rule.</summary>
    /// <param name="rule">The rule.</param>
    public void RequestDeleteRule(EmailRuleDto rule)
    {
        Confirm = new ConfirmRequest(
            "Delete rule",
            $"Delete the rule \"{rule.Name}\"?",
            "Delete",
            Danger: true,
            async () =>
            {
                await RunAsync(async () =>
                {
                    await _api.DeleteRuleAsync(rule.Id).ConfigureAwait(false);
                    await LoadRulesAsync().ConfigureAwait(false);
                    SetBanner("Rule deleted.", isSuccess: true);
                }).ConfigureAwait(false);
            });

        NotifyChanged();
    }

    /// <summary>Runs the rules over synced mail.</summary>
    /// <returns>A task that completes when the run finished.</returns>
    public async Task RunRulesAsync()
    {
        await RunAsync(async () =>
        {
            var executed = await _api.RunRulesAsync(SelectedAccountId).ConfigureAwait(false);
            SetBanner($"Rules ran on {executed} message(s).", isSuccess: true);
        }).ConfigureAwait(false);
    }

    /// <summary>Leaves the rule editor without saving.</summary>
    public void CancelRule()
    {
        Rule = null;
        View = EmailView.Rules;
        NotifyChanged();
    }

    // ── File browser ────────────────────────────────────────

    /// <summary>Asks the user to pick a file or folder from their cloud files.</summary>
    /// <param name="mode">Whether to pick a file or a folder.</param>
    /// <returns>The choice, or <see langword="null"/> when the user cancelled.</returns>
    public async Task<FileChoice?> PickFromFilesAsync(FileBrowserMode mode)
    {
        var browser = new FileBrowserState { Mode = mode };
        FileBrowser = browser;

        // Created before the first folder load: the user can press Cancel while a slow listing is in
        // flight, and a continuation installed afterwards would never complete.
        var completion = new TaskCompletionSource<FileChoice?>(TaskCreationOptions.RunContinuationsAsynchronously);
        browserCompletion = completion;

        NotifyChanged();
        await LoadFileBrowserFolderAsync(browser, null).ConfigureAwait(false);

        return await completion.Task.ConfigureAwait(false);
    }

    /// <summary>Navigates the file browser to a folder.</summary>
    /// <param name="folderId">The folder id, or <see langword="null"/> for the root.</param>
    /// <param name="folderName">The folder's display name.</param>
    /// <param name="depth">How many breadcrumbs to keep.</param>
    /// <returns>A task that completes when the folder is listed.</returns>
    public async Task NavigateFileBrowserAsync(Guid? folderId, string folderName, int depth)
    {
        if (FileBrowser is not { } browser)
        {
            return;
        }

        if (browser.Trail.Count > depth)
        {
            browser.Trail.RemoveRange(depth, browser.Trail.Count - depth);
        }

        if (folderId is { } id)
        {
            browser.Trail.Add((id, folderName));
        }

        browser.Selected = null;
        await LoadFileBrowserFolderAsync(browser, folderId).ConfigureAwait(false);
    }

    /// <summary>Selects a file in the browser, or enters a folder.</summary>
    /// <param name="node">The clicked node.</param>
    /// <returns>A task that completes when the click was handled.</returns>
    public async Task SelectFileNodeAsync(EmailFileNodeDto node)
    {
        if (FileBrowser is not { } browser)
        {
            return;
        }

        if (node.NodeType is "Folder" or "SymbolicLink")
        {
            await NavigateFileBrowserAsync(node.Id, node.Name, browser.Trail.Count).ConfigureAwait(false);
            return;
        }

        browser.Selected = node;
        NotifyChanged();
    }

    /// <summary>Accepts the file browser's choice.</summary>
    /// <returns>A task that completes when the overlay closed.</returns>
    public Task ConfirmFileBrowserAsync()
    {
        if (FileBrowser is not { } browser)
        {
            return Task.CompletedTask;
        }

        FileChoice? choice = browser.Mode == FileBrowserMode.Folder
            ? new FileChoice(browser.Current?.Id, browser.Current?.Name ?? "Files", null)
            : browser.Selected is { } node
                ? new FileChoice(null, "Files", node)
                : null;

        FileBrowser = null;
        NotifyChanged();
        browserCompletion?.TrySetResult(choice);
        return Task.CompletedTask;
    }

    /// <summary>Cancels the file browser.</summary>
    /// <returns>A task that completes when the overlay closed.</returns>
    public Task CancelFileBrowserAsync()
    {
        FileBrowser = null;
        NotifyChanged();
        browserCompletion?.TrySetResult(null);
        return Task.CompletedTask;
    }

    private async Task LoadFileBrowserFolderAsync(FileBrowserState browser, Guid? folderId)
    {
        browser.Loading = true;
        browser.Error = null;
        NotifyChanged();

        try
        {
            browser.Nodes = [.. await _api.ListFilesAsync(folderId).ConfigureAwait(false)];
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            browser.Error = DescribeFileError(ex);
        }
        finally
        {
            browser.Loading = false;
            NotifyChanged();
        }
    }

    // ── Loading helpers ─────────────────────────────────────

    private async Task LoadAccountsAsync(CancellationToken cancellationToken = default)
    {
        Accounts.Clear();
        Accounts.AddRange(await _api.ListAccountsAsync(cancellationToken).ConfigureAwait(false));

        if (Accounts.Count == 0)
        {
            SelectedAccountId = null;
            Mailboxes.Clear();
            SelectedMailboxId = null;
            Threads.Clear();
            return;
        }

        if (Accounts.All(account => account.Id != SelectedAccountId))
        {
            SelectedAccountId = Accounts[0].Id;
        }

        await LoadMailboxesAsync(selectInbox: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task LoadMailboxesAsync(bool selectInbox, CancellationToken cancellationToken = default)
    {
        if (SelectedAccount is not { } account)
        {
            Mailboxes.Clear();
            SelectedMailboxId = null;
            Threads.Clear();
            return;
        }

        Mailboxes.Clear();
        Mailboxes.AddRange(SortMailboxes(await _api.ListMailboxesAsync(account.Id, cancellationToken).ConfigureAwait(false)));

        var stillThere = Mailboxes.Any(mailbox => mailbox.Id == SelectedMailboxId);
        if (selectInbox || !stillThere)
        {
            var inbox = Mailboxes.FirstOrDefault(mailbox => (mailbox.SyncFlags & InboxFlag) != 0);
            SelectedMailboxId = inbox?.Id ?? Mailboxes.FirstOrDefault()?.Id;
            Threads.Clear();
            SelectedThread = null;
            Messages.Clear();

            if (SelectedMailboxId is not null)
            {
                await LoadThreadsAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task LoadThreadsAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedAccount is not { } account || SelectedMailbox is not { } mailbox)
        {
            Threads.Clear();
            return;
        }

        Threads.Clear();
        Threads.AddRange(await _api.ListThreadsAsync(account.Id, mailbox.Id, cancellationToken).ConfigureAwait(false));
    }

    private async Task LoadRulesAsync(CancellationToken cancellationToken = default)
    {
        Rules.Clear();
        Rules.AddRange(await _api.ListRulesAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task RunAsync(Func<Task> action)
    {
        Busy = true;
        BannerText = null;
        NotifyChanged();

        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            SetBanner(DescribeFileError(ex), isSuccess: false);
        }
        finally
        {
            Busy = false;
            NotifyChanged();
        }
    }

    /// <summary>Shows a failure in the banner.</summary>
    /// <param name="message">The message.</param>
    public void ReportError(string message) => SetBanner(message, isSuccess: false);

    /// <summary>Shows a success in the banner.</summary>
    /// <param name="message">The message.</param>
    public void ReportSuccess(string message) => SetBanner(message, isSuccess: true);

    private void SetBanner(string message, bool isSuccess)
    {
        BannerText = message;
        BannerIsSuccess = isSuccess;
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();

    /// <summary>
    /// Turns a failed file call into something the user can act on, and asks the shell for the
    /// consent scope a refusal is missing.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The message to show.</returns>
    public string DescribeFileError(Exception exception)
    {
        if (exception is ModuleConsentRequiredException consent)
        {
            _ = _bridge.RequestConsentAsync(consent.Scopes.ToArray());
            return "DotNetCloud needs your approval to use your files. Approve the request it just showed, then try again.";
        }

        var code = (exception as ModuleApiException)?.ErrorCode;

        return code switch
        {
            "FILES_UNAVAILABLE" => "Your files are not reachable right now — the Files module may not be running.",
            "FILE_NOT_FOUND" => "That file is no longer there. Refresh the list and pick another one.",
            _ => exception.Message,
        };
    }

    private string AbsoluteUrl(string relative) => new Uri(new Uri(_navigation.BaseUri), relative).ToString();

    private static List<EmailMailboxDto> SortMailboxes(IReadOnlyList<EmailMailboxDto> mailboxes) =>
        [.. mailboxes
            .OrderBy(Rank)
            .ThenBy(mailbox => mailbox.SortOrder)];

    private static int Rank(EmailMailboxDto mailbox)
    {
        var flags = mailbox.SyncFlags;

        return (flags & InboxFlag) != 0 ? 0
            : (flags & SentFlag) != 0 ? 1
            : (flags & DraftsFlag) != 0 ? 2
            : 10;
    }

    /// <summary>Parses one of the host's address columns.</summary>
    /// <param name="json">The JSON array of addresses.</param>
    /// <returns>The addresses; empty when the column is unusable.</returns>
    public static IReadOnlyList<EmailAddressDto> ParseAddresses(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<EmailAddressDto>>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Formats the sender(s) of a message.</summary>
    /// <param name="json">The host's <c>fromJson</c> column.</param>
    /// <returns>The display name, or a placeholder.</returns>
    public static string FormatFrom(string? json)
    {
        var addresses = ParseAddresses(json);
        if (addresses.Count == 0)
        {
            return "Unknown sender";
        }

        var first = addresses[0];
        return string.IsNullOrWhiteSpace(first.Name) ? first.Email : first.Name;
    }

    /// <summary>Formats a thread's participants.</summary>
    /// <param name="json">The host's <c>participantsJson</c> column.</param>
    /// <returns>Up to three participants, or a placeholder.</returns>
    public static string FormatParticipants(string? json)
    {
        var addresses = ParseAddresses(json);
        if (addresses.Count == 0)
        {
            return "Unknown";
        }

        return string.Join(
            ", ",
            addresses.Take(3).Select(address => string.IsNullOrWhiteSpace(address.Name) ? address.Email : address.Name));
    }

    /// <summary>
    /// Parses <c>Name &lt;a@b&gt;, c@d</c> into the address shape the send endpoint expects.
    /// </summary>
    /// <param name="input">The raw field text.</param>
    /// <returns>The addresses.</returns>
    public static IReadOnlyList<object> ParseRecipients(string? input)
    {
        var result = new List<object>();

        foreach (var part in (input ?? string.Empty).Split(','))
        {
            var value = part.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            var match = System.Text.RegularExpressions.Regex.Match(value, "^(.*?)\\s*<(.+?)>$");
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim().Trim('"');
                result.Add(new { name = name.Length == 0 ? null : name, email = match.Groups[2].Value.Trim() });
            }
            else
            {
                result.Add(new { name = (string?)null, email = value });
            }
        }

        return result;
    }
}

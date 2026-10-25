using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Email;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Email.UI;

/// <summary>Code-behind for the <see cref="EmailPage"/> component.</summary>
public partial class EmailPage : ComponentBase
{
    private bool _sidebarCollapsed;
    private bool _isLoading = true;
    private bool _isReply;
    private bool _isSending;
    private string? _errorMessage;

    private List<EmailAccount> _accounts = [];
    private List<EmailMailbox> _mailboxes = [];
    private List<EmailThread> _threads = [];
    private List<EmailMessage> _threadMessages = [];
    private readonly Dictionary<Guid, string> _expandedMessageBodies = [];
    private Guid? _loadingBodyMessageId;
    private bool _isSyncing;
    private List<EmailRule> _rules = [];

    [Parameter]
    public string? ThreadId { get; set; }

    private Guid? _lastHandledThreadId;

    private EmailAccount? _selectedAccount;
    private Guid? _selectedAccountId;
    private EmailMailbox? _selectedMailbox;
    private EmailThread? _selectedThread;
    private EmailRule? _editingRule;

    private EmailSendRequest? _composing;
    private bool _showRules;

    // Detach state
    private Guid? _detachingAttachmentId;

    // Add account state
    private bool _showAddAccount;
    private string _addAccountType = "imap";
    private string _imapDisplayName = string.Empty;
    private string _imapEmail = string.Empty;
    private string _imapHost = string.Empty;
    private int _imapPort = 993;
    private string _smtpHost = string.Empty;
    private int _smtpPort = 587;
    private string _imapUser = string.Empty;
    private string _imapPassword = string.Empty;
    private bool _gmailOAuthConfigured;
    private string? _gmailMessage;
    private bool _confirmDeleteAccount;

    protected override async Task OnParametersSetAsync()
    {
        // Runs after OnInitializedAsync on first render and again whenever the
        // parent re-supplies parameters (e.g. a changed ?threadId= query value).
        await HandleThreadDeepLinkAsync();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var collapsed = await JSRuntime.InvokeAsync<string>("localStorage.getItem",
                new object?[] { "dotnetcloud.sidebar:email" });
            if (bool.TryParse(collapsed ?? "false", out var parsed))
                _sidebarCollapsed = parsed;
        }
        catch { }

        // Check for Gmail OAuth callback parameters
        var uri = new Uri(_navigation.Uri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        if (query.TryGetValue("gmail", out var gmailStatus))
        {
            if (gmailStatus == "connected")
            {
                _gmailMessage = "Gmail account connected successfully!";
            }
            else if (gmailStatus == "denied")
            {
                _gmailMessage = "Gmail authorization was cancelled.";
            }
            else if (gmailStatus == "error")
            {
                query.TryGetValue("reason", out var reasonValues);
                var reason = reasonValues.ToString();
                _gmailMessage = reason switch
                {
                    "not_configured" => "Gmail OAuth is not configured by the server administrator.",
                    "token_exchange_failed" => "Failed to contact Google. Please try again.",
                    "google_rejected" => "Google rejected the authorization. The code may have expired.",
                    "state_mismatch" => "Security check failed. Please try again.",
                    "invalid_callback" => "Invalid response from Google. Please try again.",
                    _ => "Gmail OAuth failed. Please try again."
                };
            }
        }

        try
        {
            _accounts = (await ApiClient.ListAccountsAsync()).ToList();
            _rules = (await ApiClient.ListRulesAsync()).ToList();
            if (_accounts.Count > 0)
            {
                _selectedAccount = _accounts[0];
                _selectedAccountId = _accounts[0].Id;
                await LoadMailboxesAsync();
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load: {ex.Message}";
        }

        // Check Gmail OAuth status (non-blocking, best-effort)
        try
        {
            _gmailOAuthConfigured = await ApiClient.CheckGmailOAuthConfiguredAsync();
        }
        catch { _gmailOAuthConfigured = false; }

        _isLoading = false;
    }

    private void ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        try
        {
            _ = JSRuntime.InvokeVoidAsync("localStorage.setItem",
                new object?[] { "dotnetcloud.sidebar:email", _sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch { }
    }

    private async Task OnAccountSelected()
    {
        if (_selectedAccountId is null) return;
        _selectedAccount = _accounts.FirstOrDefault(a => a.Id == _selectedAccountId.Value);
        _selectedMailbox = null;
        _selectedThread = null;
        _threads.Clear();
        await LoadMailboxesAsync();
    }

    private async Task LoadMailboxesAsync()
    {
        if (_selectedAccount is null) return;
        try
        {
            _mailboxes = (await ApiClient.ListMailboxesAsync(_selectedAccount.Id))
                .OrderBy(mb =>
                {
                    if ((mb.SyncFlags & (int)MailboxFlags.Inbox) != 0) return 0;
                    if ((mb.SyncFlags & (int)MailboxFlags.Sent) != 0) return 1;
                    if ((mb.SyncFlags & (int)MailboxFlags.Drafts) != 0) return 2;
                    return 10;
                })
                .ThenBy(mb => mb.SortOrder)
                .ToList();

            // Auto-select Inbox if no mailbox is currently selected
            if (_selectedMailbox is null)
            {
                var inbox = _mailboxes.FirstOrDefault(mb =>
                    (mb.SyncFlags & (int)MailboxFlags.Inbox) != 0);
                if (inbox is not null)
                {
                    await SelectMailbox(inbox);
                }
                else if (_mailboxes.Count > 0)
                {
                    await SelectMailbox(_mailboxes[0]);
                }
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load mailboxes: {ex.Message}";
        }
    }

    private async Task SelectMailbox(EmailMailbox mailbox)
    {
        _selectedMailbox = mailbox;
        _selectedThread = null;
        _showRules = false;
        _composing = null;

        if (_selectedAccount is null) return;
        try
        {
            _threads = (await ApiClient.ListThreadsAsync(_selectedAccount.Id, mailbox.Id)).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load messages: {ex.Message}";
        }
    }

    private async Task SelectThread(EmailThread thread)
    {
        _selectedThread = thread;
        _expandedMessageBodies.Clear();
        _loadingBodyMessageId = null;
        try
        {
            _threadMessages = (await ApiClient.ListThreadMessagesAsync(thread.Id)).ToList();

            // Load all message bodies upfront — no expand/collapse, messages always shown in full
            foreach (var msg in _threadMessages)
            {
                _ = LoadMessageBodyAsync(msg.Id);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load messages: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the thread referenced by the <c>?threadId=</c> deep link when the page's
    /// <see cref="ThreadId"/> parameter points at a valid thread. Guards against
    /// re-handling the same deep link on repeated parameter sets / re-renders.
    /// </summary>
    private async Task HandleThreadDeepLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(ThreadId))
        {
            _lastHandledThreadId = null;
            return;
        }

        if (!Guid.TryParse(ThreadId, out var threadId))
        {
            _logger.LogWarning("Email deep link ignored: threadId '{ThreadId}' is not a valid id.", ThreadId);
            return;
        }

        // Already handled this exact deep link.
        if (_lastHandledThreadId == threadId)
            return;

        // Accounts must be loaded before ownership can be resolved. On the first
        // render OnParametersSetAsync runs after OnInitializedAsync, so by the time
        // we get here the accounts (and current mailbox threads) are available.
        if (_accounts.Count == 0)
            return;

        try
        {
            if (await OpenThreadDeepLinkAsync(threadId))
                _lastHandledThreadId = threadId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email deep link to thread {ThreadId} failed", threadId);
        }
    }

    /// <summary>
    /// Resolves and selects the account/mailbox that owns <paramref name="threadId"/> and
    /// opens the thread pane. Checks the already-loaded mailbox first, then scans the rest
    /// of the user's accounts/mailboxes via this page's own API client (which has no
    /// single-thread lookup). Returns true when the thread was opened.
    /// </summary>
    private async Task<bool> OpenThreadDeepLinkAsync(Guid threadId)
    {
        // Cheap path: the thread may already be loaded for the current mailbox.
        if (_selectedAccount is not null && _selectedMailbox is not null)
        {
            var current = _threads.FirstOrDefault(t => t.Id == threadId);
            if (current is not null)
            {
                await SelectThread(current);
                return true;
            }
        }

        // Fallback: scan every account/mailbox until the thread is found.
        foreach (var account in _accounts)
        {
            List<EmailMailbox> mailboxes;
            try
            {
                mailboxes = (await ApiClient.ListMailboxesAsync(account.Id)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email deep link: failed to list mailboxes for account {AccountId}", account.Id);
                continue;
            }

            foreach (var mailbox in mailboxes)
            {
                List<EmailThread> threads;
                try
                {
                    threads = (await ApiClient.ListThreadsAsync(account.Id, mailbox.Id)).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Email deep link: failed to list threads for mailbox {MailboxId}", mailbox.Id);
                    continue;
                }

                var match = threads.FirstOrDefault(t => t.Id == threadId);
                if (match is null)
                    continue;

                // Select the owning account and mailbox, then open the thread pane.
                _selectedAccount = account;
                _selectedAccountId = account.Id;
                _mailboxes = mailboxes;
                _selectedMailbox = mailbox;
                _threads = threads;
                _showRules = false;
                _composing = null;

                await SelectThread(match);
                return true;
            }
        }

        _logger.LogWarning("Email deep link: thread {ThreadId} was not found in any mailbox", threadId);
        return false;
    }

    private async Task LoadMessageBodyAsync(Guid messageId)
    {
        if (_expandedMessageBodies.ContainsKey(messageId))
            return;

        _loadingBodyMessageId = messageId;
        StateHasChanged();
        try
        {
            var html = await ApiClient.GetMessageBodyAsync(messageId);
            _expandedMessageBodies[messageId] = html ?? string.Empty;
        }
        catch (Exception ex)
        {
            _expandedMessageBodies[messageId] = $"<p class=\"email-body-error\">Failed to load message content: {ex.Message}</p>";
        }
        finally
        {
            if (_loadingBodyMessageId == messageId)
                _loadingBodyMessageId = null;
            StateHasChanged();
        }
    }

    private void BeginCompose()
    {
        if (_selectedAccount is null) return;
        _isReply = false;
        _composing = new EmailSendRequest
        {
            To = [],
            Subject = string.Empty
        };
    }

    private void ReplyToThread(EmailThread thread)
    {
        if (_selectedAccount is null) return;
        _isReply = true;
        _composing = new EmailSendRequest
        {
            To = [],
            Subject = $"Re: {thread.Subject}"
        };
    }

    private void ForwardThread(EmailThread thread)
    {
        if (_selectedAccount is null) return;
        _isReply = true;
        _composing = new EmailSendRequest
        {
            To = [],
            Subject = $"Fwd: {thread.Subject}"
        };
    }

    private async Task SendEmailAsync(EmailSendRequest request)
    {
        _logger.LogInformation("SendEmailAsync called. _selectedAccount is null={Null}, accountId={Id}, To={To}, Subject={Subj}",
            _selectedAccount is null, _selectedAccount?.Id, request.To?.Count, request.Subject);

        if (_selectedAccount is null)
        {
            _logger.LogWarning("SendEmailAsync: _selectedAccount is null, aborting");
            return;
        }

        _isSending = true;
        StateHasChanged();

        try
        {
            _logger.LogInformation("SendEmailAsync: calling ApiClient.SendAsync for account {Id}", _selectedAccount.Id);
            await ApiClient.SendAsync(_selectedAccount.Id, request);
            _logger.LogInformation("SendEmailAsync: ApiClient.SendAsync succeeded");
            _composing = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendEmailAsync: ApiClient.SendAsync failed");
            _errorMessage = $"Failed to send: {ex.Message}";
        }
        finally
        {
            _isSending = false;
            StateHasChanged();
        }
    }

    private void CancelCompose()
    {
        _composing = null;
        _isReply = false;
        StateHasChanged();
    }

    private void BeginAddAccount()
    {
        _showAddAccount = true;
        _showRules = false;
        _composing = null;
    }

    private void CancelAddAccount()
    {
        _showAddAccount = false;
    }

    private async Task AddImapAccount()
    {
        try
        {
            var creds = System.Text.Json.JsonSerializer.Serialize(new
            {
                imapHost = _imapHost,
                imapPort = _imapPort,
                imapUseSsl = true,
                smtpHost = _smtpHost,
                smtpPort = _smtpPort,
                smtpUseStartTls = _smtpPort != 465,
                username = _imapUser,
                password = _imapPassword
            });

            await ApiClient.CreateAccountAsync(new CreateEmailAccountRequest
            {
                ProviderType = EmailProviderType.ImapSmtp,
                DisplayName = _imapDisplayName,
                EmailAddress = _imapEmail,
                CredentialsJson = creds
            });

            _showAddAccount = false;
            _accounts = (await ApiClient.ListAccountsAsync()).ToList();
            if (_accounts.Count == 1)
            {
                _selectedAccount = _accounts[0];
                _selectedAccountId = _accounts[0].Id;
                await LoadMailboxesAsync();
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to add account: {ex.Message}";
        }
    }

    private async Task StartGmailOAuth()
    {
        // Navigate the browser to the OAuth start endpoint, which will
        // redirect the user to Google for authorization.
        _navigation.NavigateTo("/api/v1/email/gmail/oauth/start", forceLoad: true);
    }

    private async Task SyncAccount()
    {
        if (_selectedAccount is null) return;
        _isSyncing = true;
        _errorMessage = null;
        try
        {
            await ApiClient.TriggerSyncAsync(_selectedAccount.Id);

            // Reload mailboxes and current view after sync
            _mailboxes = (await ApiClient.ListMailboxesAsync(_selectedAccount.Id)).ToList();
            if (_selectedMailbox is not null)
            {
                _threads = (await ApiClient.ListThreadsAsync(_selectedAccount.Id, _selectedMailbox.Id)).ToList();
            }
            _selectedThread = null;
            _threadMessages.Clear();
            _expandedMessageBodies.Clear();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task ConfirmDeleteAccount()
    {
        if (_selectedAccount is null) return;
        try
        {
            await ApiClient.DeleteAccountAsync(_selectedAccount.Id);
            _confirmDeleteAccount = false;
            _accounts = (await ApiClient.ListAccountsAsync()).ToList();
            _selectedAccount = null;
            _selectedAccountId = Guid.Empty;
            _mailboxes.Clear();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to delete account: {ex.Message}";
        }
    }

    private void BeginAddRule()
    {
        _editingRule = new EmailRule
        {
            IsEnabled = true,
            Priority = _rules.Count + 1
        };
    }

    private void CancelEditRule()
    {
        _editingRule = null;
        StateHasChanged();
    }

    private async Task SaveRuleAsync(EmailRule rule)
    {
        try
        {
            if (rule.Id == Guid.Empty)
            {
                await ApiClient.CreateRuleAsync(new CreateEmailRuleRequest
                {
                    Name = rule.Name,
                    AccountId = _selectedAccountId,
                    IsEnabled = rule.IsEnabled,
                    Priority = rule.Priority,
                    StopProcessing = rule.StopProcessing,
                    ConditionGroups = [],
                    Actions = []
                });
            }
            else
            {
                await ApiClient.UpdateRuleAsync(rule.Id, new UpdateEmailRuleRequest
                {
                    Name = rule.Name,
                    IsEnabled = rule.IsEnabled,
                    Priority = rule.Priority,
                    StopProcessing = rule.StopProcessing
                });
            }
            _editingRule = null;
            _rules = (await ApiClient.ListRulesAsync(_selectedAccountId)).ToList();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save rule: {ex.Message}";
            StateHasChanged();
        }
    }

    private async Task DeleteRule(EmailRule rule)
    {
        try
        {
            await ApiClient.DeleteRuleAsync(rule.Id);
            _rules = (await ApiClient.ListRulesAsync(_selectedAccountId)).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to delete rule: {ex.Message}";
        }
    }

    private async Task RunRules()
    {
        try
        {
            await ApiClient.RunRulesAsync(_selectedAccountId);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to run rules: {ex.Message}";
        }
    }

    private async Task DetachAttachment(Guid attachmentId)
    {
        _detachingAttachmentId = attachmentId;
        StateHasChanged();
        try
        {
            await ApiClient.DetachAttachmentAsync(attachmentId);
            _logger.LogInformation("Attachment {AttachmentId} detached to Files module", attachmentId);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save to Files: {ex.Message}";
        }
        finally
        {
            _detachingAttachmentId = null;
            StateHasChanged();
        }
    }

    private static string MailboxIcon(EmailMailbox mb)
    {
        if ((mb.SyncFlags & (int)MailboxFlags.Inbox) != 0) return "move_to_inbox";
        if ((mb.SyncFlags & (int)MailboxFlags.Sent) != 0) return "outbox";
        if ((mb.SyncFlags & (int)MailboxFlags.Drafts) != 0) return "edit_note";
        if ((mb.SyncFlags & (int)MailboxFlags.Trash) != 0) return "delete";
        if ((mb.SyncFlags & (int)MailboxFlags.Spam) != 0) return "block";
        if ((mb.SyncFlags & (int)MailboxFlags.Archive) != 0) return "inventory_2";
        return "folder";
    }

    private static string FormatFrom(string fromJson)
    {
        try
        {
            var addresses = System.Text.Json.JsonSerializer.Deserialize<List<EmailAddressDto>>(fromJson);
            if (addresses is { Count: > 0 })
                return addresses[0].Name ?? addresses[0].Email;
        }
        catch { }
        return fromJson;
    }

    private static string FormatParticipants(string participantsJson)
    {
        try
        {
            var addresses = System.Text.Json.JsonSerializer.Deserialize<List<EmailAddressDto>>(participantsJson);
            if (addresses is { Count: > 0 })
                return string.Join(", ", addresses.Take(3).Select(a => a.Name ?? a.Email));
        }
        catch { }
        return "Unknown";
    }

    /// <summary>
    /// Returns a file type icon emoji based on the MIME content type.
    /// </summary>
    private static string GetFileTypeIcon(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return "attach_file";

        var type = contentType.ToLowerInvariant();

        if (type.StartsWith("image/"))
            return "image";
        if (type == "application/pdf")
            return "picture_as_pdf";
        if (type is "application/zip" or "application/x-rar-compressed"
            or "application/gzip" or "application/x-7z-compressed"
            or "application/x-tar")
            return "folder_zip";
        if (type.StartsWith("text/"))
            return "text_snippet";
        if (type.StartsWith("audio/"))
            return "music_note";
        if (type.StartsWith("video/"))
            return "movie";
        if (type.Contains("spreadsheet") || type.Contains("excel") || type.Contains("csv"))
            return "table_chart";
        if (type.Contains("presentation") || type.Contains("powerpoint") || type.Contains("slides"))
            return "slideshow";
        if (type.Contains("word") || type.Contains("document"))
            return "article";

        return "attach_file";
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";

        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    /// <summary>
    /// Converts a plain-text email body (with <c>&gt;</c> quote markers) into formatted HTML.
    /// Used as a fallback when <see cref="EmailMessage.BodyHtml"/> is null.
    /// </summary>
    private static string ConvertPlainTextToHtml(string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return string.Empty;

        var lines = plainText.Split('\n');
        var html = new System.Text.StringBuilder();
        var quoteDepth = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Detect quote depth from leading '>' characters
            var depth = 0;
            var content = line;
            while (content.StartsWith('>'))
            {
                depth++;
                content = content.AsSpan(1).TrimStart().ToString();
            }

            if (depth > 0)
            {
                // Close excess quote levels
                while (quoteDepth > depth)
                {
                    html.AppendLine("</blockquote>");
                    quoteDepth--;
                }
                // Open needed quote levels
                while (quoteDepth < depth)
                {
                    html.AppendLine("<blockquote>");
                    quoteDepth++;
                }
                // Render quoted content line
                var escaped = System.Net.WebUtility.HtmlEncode(content);
                escaped = Linkify(escaped);
                html.Append("<p>").Append(escaped).AppendLine("</p>");
            }
            else
            {
                // Close all quotes when returning to unquoted text
                while (quoteDepth > 0)
                {
                    html.AppendLine("</blockquote>");
                    quoteDepth--;
                }

                // Separator lines (dashes, underscores, equals)
                if (line.Length >= 3 && line.All(c => c is '-' or '=' or '_'))
                {
                    html.AppendLine("<hr>");
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    html.AppendLine("<br>");
                }
                else
                {
                    var escaped = System.Net.WebUtility.HtmlEncode(line);
                    escaped = Linkify(escaped);
                    html.Append("<p>").Append(escaped).AppendLine("</p>");
                }
            }
        }

        // Close any remaining open quotes
        while (quoteDepth > 0)
        {
            html.AppendLine("</blockquote>");
            quoteDepth--;
        }

        return html.ToString().TrimEnd();
    }

    /// <summary>
    /// Converts bare URLs in escaped text to HTML anchor tags.
    /// Must be called on already-HTML-escaped text.
    /// </summary>
    private static string Linkify(string escaped)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            escaped,
            @"(https?://[^\s<>""'()]+)",
            "<a href=\"$1\" target=\"_blank\" rel=\"noopener noreferrer\">$1</a>"
        );
    }
}

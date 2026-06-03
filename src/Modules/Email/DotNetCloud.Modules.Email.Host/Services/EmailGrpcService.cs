using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Email.Host.Protos;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Grpc.Core;

namespace DotNetCloud.Modules.Email.Host.Services;

/// <summary>
/// gRPC service implementation for the Email module.
/// </summary>
public sealed class EmailGrpcService : EmailService.EmailServiceBase
{
    private readonly IEmailAccountService _accountService;
    private readonly IEmailSendService _sendService;
    private readonly IEmailRuleService _ruleService;
    private readonly ILogger<EmailGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailGrpcService"/> class.
    /// </summary>
    public EmailGrpcService(
        IEmailAccountService accountService,
        IEmailSendService sendService,
        IEmailRuleService ruleService,
        ILogger<EmailGrpcService> logger)
    {
        _accountService = accountService;
        _sendService = sendService;
        _ruleService = ruleService;
        _logger = logger;
    }

    private static CallerContext SystemCaller => CallerContext.CreateSystemContext();

    // ─── Accounts ───────────────────────────────────────────────────────────

    public override async Task<ListAccountsResponse> ListAccounts(
        ListAccountsRequest request, ServerCallContext context)
    {
        try
        {
            var accounts = await _accountService.ListAsync(SystemCaller, context.CancellationToken);
            var response = new ListAccountsResponse { Success = true };
            response.Accounts.AddRange(accounts.Select(ToAccountMessage));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListAccounts gRPC failed");
            return new ListAccountsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<AccountResponse> GetAccount(
        GetAccountRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var id))
            return new AccountResponse { Success = false, ErrorMessage = "Invalid ID." };

        var account = await _accountService.GetAsync(id, SystemCaller, context.CancellationToken);
        return account is null
            ? new AccountResponse { Success = false, ErrorMessage = "Not found." }
            : new AccountResponse { Success = true, Account = ToAccountMessage(account) };
    }

    public override async Task<AccountResponse> CreateAccount(
        CreateAccountRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateEmailAccountRequest
            {
                ProviderType = Enum.TryParse<EmailProviderType>(request.ProviderType, true, out var pt) ? pt : EmailProviderType.ImapSmtp,
                DisplayName = request.DisplayName,
                EmailAddress = request.EmailAddress,
                CredentialsJson = string.IsNullOrEmpty(request.CredentialsJson) ? null : request.CredentialsJson
            };
            var account = await _accountService.CreateAsync(dto, SystemCaller, context.CancellationToken);
            return new AccountResponse { Success = true, Account = ToAccountMessage(account) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateAccount gRPC failed");
            return new AccountResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<AccountResponse> UpdateAccount(
        UpdateAccountRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var id))
            return new AccountResponse { Success = false, ErrorMessage = "Invalid ID." };

        try
        {
            var dto = new UpdateEmailAccountRequest
            {
                DisplayName = string.IsNullOrEmpty(request.DisplayName) ? null : request.DisplayName,
                IsEnabled = request.IsEnabled
            };
            var account = await _accountService.UpdateAsync(id, dto, SystemCaller, context.CancellationToken);
            return new AccountResponse { Success = true, Account = ToAccountMessage(account) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAccount gRPC failed");
            return new AccountResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<DeleteAccountResponse> DeleteAccount(
        DeleteAccountRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var id))
            return new DeleteAccountResponse { Success = false, ErrorMessage = "Invalid ID." };

        try
        {
            await _accountService.DeleteAsync(id, SystemCaller, context.CancellationToken);
            return new DeleteAccountResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAccount gRPC failed");
            return new DeleteAccountResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Mailboxes ──────────────────────────────────────────────────────────

    public override async Task<ListMailboxesResponse> ListMailboxes(
        ListMailboxesRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            return new ListMailboxesResponse { Success = false };

        try
        {
            var mailboxes = await _accountService.ListMailboxesAsync(accountId, SystemCaller, context.CancellationToken);
            var response = new ListMailboxesResponse { Success = true };
            response.Mailboxes.AddRange(mailboxes.Select(ToMailboxMessage));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListMailboxes gRPC failed");
            return new ListMailboxesResponse { Success = false };
        }
    }

    // ─── Threads & Messages ─────────────────────────────────────────────────

    public override Task<ListThreadsResponse> ListThreads(
        ListThreadsRequest request, ServerCallContext context)
    {
        // Thread listing via gRPC: currently delegates to account service for mailbox listing.
        // Full thread listing requires an email provider, which is not exposed via gRPC yet.
        _logger.LogWarning("ListThreads gRPC: not fully implemented — requires email provider access");
        return Task.FromResult(new ListThreadsResponse { Success = false, ErrorMessage = "Thread listing via gRPC not yet implemented. Use REST API." });
    }

    public override Task<ListMessagesResponse> ListThreadMessages(
        ListThreadMessagesRequest request, ServerCallContext context)
    {
        _logger.LogWarning("ListThreadMessages gRPC: not fully implemented — requires email provider access");
        return Task.FromResult(new ListMessagesResponse { Success = false, ErrorMessage = "Message listing via gRPC not yet implemented. Use REST API." });
    }

    public override Task<MessageBodyResponse> GetMessageBody(
        GetMessageBodyRequest request, ServerCallContext context)
    {
        _logger.LogWarning("GetMessageBody gRPC: not fully implemented — requires email provider access");
        return Task.FromResult(new MessageBodyResponse { Success = false });
    }

    // ─── Send ───────────────────────────────────────────────────────────────

    public override async Task<SendEmailResponse> SendEmail(
        SendEmailRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            return new SendEmailResponse { Success = false, ErrorMessage = "Invalid account ID." };

        try
        {
            var dto = new EmailSendRequest
            {
                To = request.To.Select(e => new EmailAddressDto { Email = e }).ToList(),
                Cc = request.Cc.Select(e => new EmailAddressDto { Email = e }).ToList(),
                Bcc = request.Bcc.Select(e => new EmailAddressDto { Email = e }).ToList(),
                Subject = request.Subject,
                BodyHtml = string.IsNullOrEmpty(request.BodyHtml) ? null : request.BodyHtml,
                BodyPlainText = string.IsNullOrEmpty(request.BodyText) ? null : request.BodyText,
                Attachments = request.AttachmentStorageKeys.Select(k => new EmailAttachmentRef
                {
                    StorageKey = k,
                    FileName = k,
                    ContentType = "application/octet-stream"
                }).ToList()
            };
            await _sendService.SendAsync(accountId, dto, SystemCaller, context.CancellationToken);
            return new SendEmailResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendEmail gRPC failed");
            return new SendEmailResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Sync ───────────────────────────────────────────────────────────────

    public override Task<TriggerSyncResponse> TriggerSync(
        TriggerSyncRequest request, ServerCallContext context)
    {
        // Sync trigger: the EmailSyncBackgroundService runs as a hosted service.
        // A gRPC call can request an immediate sync, but the background service
        // manages its own scheduling. For now, acknowledge the request.
        _logger.LogInformation("TriggerSync requested for account {AccountId}", request.AccountId);
        return Task.FromResult(new TriggerSyncResponse { Success = true });
    }

    // ─── Rules ──────────────────────────────────────────────────────────────

    public override async Task<ListRulesResponse> ListRules(
        ListRulesRequest request, ServerCallContext context)
    {
        try
        {
            Guid? accountId = Guid.TryParse(request.AccountId, out var aid) ? aid : null;
            var rules = await _ruleService.ListAsync(SystemCaller, accountId, context.CancellationToken);
            var response = new ListRulesResponse { Success = true };
            response.Rules.AddRange(rules.Select(ToRuleMessage));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListRules gRPC failed");
            return new ListRulesResponse { Success = false };
        }
    }

    public override async Task<RuleResponse> GetRule(
        GetRuleRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.RuleId, out var id))
            return new RuleResponse { Success = false, ErrorMessage = "Invalid ID." };

        var rule = await _ruleService.GetAsync(id, SystemCaller, context.CancellationToken);
        return rule is null
            ? new RuleResponse { Success = false, ErrorMessage = "Not found." }
            : new RuleResponse { Success = true, Rule = ToRuleMessage(rule) };
    }

    public override async Task<RuleResponse> CreateRule(
        CreateRuleRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new CreateEmailRuleRequest
            {
                Name = request.Name,
                AccountId = Guid.TryParse(request.AccountId, out var aid) ? aid : null,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                StopProcessing = request.StopProcessing,
                ConditionGroups = [],
                Actions = []
            };
            var rule = await _ruleService.CreateAsync(dto, SystemCaller, context.CancellationToken);
            return new RuleResponse { Success = true, Rule = ToRuleMessage(rule) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateRule gRPC failed");
            return new RuleResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<RuleResponse> UpdateRule(
        UpdateRuleRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.RuleId, out var id))
            return new RuleResponse { Success = false, ErrorMessage = "Invalid ID." };

        try
        {
            var dto = new UpdateEmailRuleRequest
            {
                Name = string.IsNullOrEmpty(request.Name) ? null : request.Name,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                StopProcessing = request.StopProcessing
            };
            var rule = await _ruleService.UpdateAsync(id, dto, SystemCaller, context.CancellationToken);
            return new RuleResponse { Success = true, Rule = ToRuleMessage(rule) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateRule gRPC failed");
            return new RuleResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<DeleteRuleResponse> DeleteRule(
        DeleteRuleRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.RuleId, out var id))
            return new DeleteRuleResponse { Success = false, ErrorMessage = "Invalid ID." };

        try
        {
            await _ruleService.DeleteAsync(id, SystemCaller, context.CancellationToken);
            return new DeleteRuleResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteRule gRPC failed");
            return new DeleteRuleResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<RunRulesResponse> RunRules(
        RunRulesRequest request, ServerCallContext context)
    {
        try
        {
            Guid? accountId = Guid.TryParse(request.AccountId, out var aid) ? aid : null;
            Guid? mailboxId = Guid.TryParse(request.MailboxId, out var mid) ? mid : null;
            var count = await _ruleService.RunRulesAsync(SystemCaller, accountId, mailboxId, context.CancellationToken);
            return new RunRulesResponse { Success = true, MatchedCount = count };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunRules gRPC failed");
            return new RunRulesResponse { Success = false, MatchedCount = 0 };
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static AccountMessage ToAccountMessage(EmailAccount a) => new()
    {
        Id = a.Id.ToString(),
        OwnerId = a.OwnerId.ToString(),
        ProviderType = a.ProviderType.ToString(),
        DisplayName = a.DisplayName,
        EmailAddress = a.EmailAddress,
        IsEnabled = a.IsEnabled,
        CreatedAt = a.CreatedAt.ToString("O"),
        UpdatedAt = a.UpdatedAt.ToString("O")
    };

    private static MailboxMessage ToMailboxMessage(EmailMailbox m) => new()
    {
        Id = m.Id.ToString(),
        AccountId = m.AccountId.ToString(),
        Name = m.DisplayName,
        FullName = m.ProviderId,
        MessageCount = 0,
        UnseenCount = 0,
        LastSyncedAt = m.LastSyncedAt?.ToString("O") ?? string.Empty
    };

    private static RuleMessage ToRuleMessage(EmailRule r) => new()
    {
        Id = r.Id.ToString(),
        OwnerId = r.OwnerId.ToString(),
        AccountId = r.AccountId?.ToString() ?? string.Empty,
        Name = r.Name,
        IsEnabled = r.IsEnabled,
        Priority = r.Priority,
        StopProcessing = r.StopProcessing,
        CreatedAt = r.CreatedAt.ToString("O"),
        UpdatedAt = r.UpdatedAt.ToString("O")
    };
}

using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Host.Protos;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using DotNetCloud.Modules.Files.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Email.Host.Services;

/// <summary>
/// gRPC service implementation for the Email module.
/// </summary>
public sealed class EmailGrpcService : EmailService.EmailServiceBase
{
    private readonly IEmailAccountService _accountService;
    private readonly IEmailSendService _sendService;
    private readonly IEmailRuleService _ruleService;
    private readonly IAttachmentStorage _attachmentStorage;
    private readonly EmailDbContext _db;
    private readonly ILogger<EmailGrpcService> _logger;
    private readonly Lazy<FilesService.FilesServiceClient> _filesClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailGrpcService"/> class.
    /// </summary>
    public EmailGrpcService(
        IEmailAccountService accountService,
        IEmailSendService sendService,
        IEmailRuleService ruleService,
        IAttachmentStorage attachmentStorage,
        EmailDbContext db,
        IConfiguration configuration,
        ILogger<EmailGrpcService> logger)
    {
        _accountService = accountService;
        _sendService = sendService;
        _ruleService = ruleService;
        _attachmentStorage = attachmentStorage;
        _db = db;
        _logger = logger;

        _filesClient = new Lazy<FilesService.FilesServiceClient>(() =>
        {
            var address = configuration.GetValue<string>("FilesGrpc:Address") ?? "http://localhost:5004";
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true,
                    ConnectTimeout = TimeSpan.FromSeconds(5)
                }
            });
            return new FilesService.FilesServiceClient(channel);
        });
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

    public override async Task<ListThreadsResponse> ListThreads(
        ListThreadsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId) ||
            !Guid.TryParse(request.MailboxId, out var mailboxId))
            return new ListThreadsResponse { Success = false, ErrorMessage = "Invalid ID format." };

        try
        {
            var skip = request.Skip;
            var take = request.Take > 0 && request.Take <= 100 ? request.Take : 50;

            // Find thread IDs that have at least one message in this mailbox
            var threadIdsInMailbox = await _db.EmailMessages
                .AsNoTracking()
                .Where(m => m.MailboxId == mailboxId && m.AccountId == accountId)
                .Select(m => m.ThreadId)
                .Distinct()
                .ToListAsync(context.CancellationToken);

            if (threadIdsInMailbox.Count == 0)
                return new ListThreadsResponse { Success = true };

            var threads = await _db.EmailThreads
                .AsNoTracking()
                .Where(t => threadIdsInMailbox.Contains(t.Id))
                .OrderByDescending(t => t.LastMessageAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(context.CancellationToken);

            var response = new ListThreadsResponse { Success = true };
            response.Threads.AddRange(threads.Select(ToThreadMessage));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListThreads gRPC failed");
            return new ListThreadsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<ListMessagesResponse> ListThreadMessages(
        ListThreadMessagesRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ThreadId, out var threadId))
            return new ListMessagesResponse { Success = false, ErrorMessage = "Invalid thread ID." };

        try
        {
            var messages = await _db.EmailMessages
                .AsNoTracking()
                .Include(m => m.Attachments)
                .Where(m => m.ThreadId == threadId)
                .OrderBy(m => m.DateReceived)
                .ToListAsync(context.CancellationToken);

            var response = new ListMessagesResponse { Success = true };
            response.Messages.AddRange(messages.Select(ToEmailMessageItem));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListThreadMessages gRPC failed");
            return new ListMessagesResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<MessageBodyResponse> GetMessageBody(
        GetMessageBodyRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.MessageId, out var messageId))
            return new MessageBodyResponse { Success = false };

        try
        {
            var message = await _db.EmailMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == messageId, context.CancellationToken);

            return message is null
                ? new MessageBodyResponse { Success = false }
                : new MessageBodyResponse
                {
                    Success = true,
                    BodyHtml = message.BodyHtml ?? string.Empty,
                    BodyText = message.BodyPreview ?? string.Empty
                };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMessageBody gRPC failed");
            return new MessageBodyResponse { Success = false };
        }
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

    // ─── Attachments (Streaming) ────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task DownloadAttachment(
        DownloadAttachmentRequest request,
        IServerStreamWriter<DownloadAttachmentChunk> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.AttachmentId, out var attachmentId))
        {
            _logger.LogWarning("DownloadAttachment: invalid attachment ID {Id}", request.AttachmentId);
            return;
        }

        try
        {
            var attachment = await _db.EmailAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attachmentId, context.CancellationToken);

            if (attachment is null || string.IsNullOrEmpty(attachment.StorageKey))
            {
                _logger.LogWarning("DownloadAttachment: attachment {Id} not found or missing storage key", attachmentId);
                return;
            }

            var stream = await _attachmentStorage.OpenReadAsync(attachment.StorageKey, context.CancellationToken);
            if (stream is null)
            {
                _logger.LogWarning("DownloadAttachment: storage content not found for key {Key}", attachment.StorageKey);
                return;
            }

            await using (stream)
            {
                var buffer = new byte[64 * 1024]; // 64 KB chunks
                int bytesRead;
                bool firstChunk = true;

                while ((bytesRead = await stream.ReadAsync(buffer, context.CancellationToken)) > 0)
                {
                    var chunk = new DownloadAttachmentChunk
                    {
                        Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead),
                        FileName = firstChunk ? (attachment.FileName ?? string.Empty) : string.Empty,
                        ContentType = firstChunk ? (attachment.ContentType ?? "application/octet-stream") : string.Empty,
                        TotalSize = firstChunk ? attachment.Size : 0L
                    };
                    await responseStream.WriteAsync(chunk, context.CancellationToken);
                    firstChunk = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DownloadAttachment gRPC failed for {AttachmentId}", request.AttachmentId);
        }
    }

    /// <inheritdoc />
    public override async Task<UploadAttachmentResponse> UploadAttachment(
        IAsyncStreamReader<UploadAttachmentChunk> requestStream,
        ServerCallContext context)
    {
        string? fileName = null;
        string? contentType = null;

        await using var ms = new MemoryStream();
        try
        {
            bool firstChunk = true;
            await foreach (var chunk in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (firstChunk)
                {
                    fileName = chunk.FileName;
                    contentType = chunk.ContentType;
                    firstChunk = false;
                }
                ms.Write(chunk.Data.Span);
            }

            if (ms.Length == 0)
                return new UploadAttachmentResponse { Success = false, ErrorMessage = "No data received." };

            ms.Position = 0;
            var result = await _attachmentStorage.StoreAsync(
                ms,
                fileName ?? "attachment.bin",
                contentType ?? "application/octet-stream",
                context.CancellationToken);

            return new UploadAttachmentResponse
            {
                Success = true,
                StorageKey = result.StorageKey,
                FileName = fileName ?? "attachment.bin",
                ContentType = contentType ?? "application/octet-stream",
                Size = result.Size,
                ContentHash = result.ContentHash
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UploadAttachment gRPC failed");
            return new UploadAttachmentResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override Task<DetachAttachmentResponse> DetachAttachment(
        DetachAttachmentRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.AttachmentId, out var attachmentId))
            return Task.FromResult(new DetachAttachmentResponse { Success = false, ErrorMessage = "Invalid attachment ID." });

        // Detach is acknowledged here; the actual detach operation (moving content
        // to the Files module) is handled by a cross-module event or explicit API call.
        _logger.LogInformation("DetachAttachment requested for {AttachmentId}, target folder {FolderId}",
            attachmentId, request.TargetFolderId);

        return Task.FromResult(new DetachAttachmentResponse { Success = true });
    }

    /// <inheritdoc />
    public override async Task<UploadAttachmentResponse> AttachFromFilesModule(
        AttachFromFilesRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.FileNodeId, out var fileNodeId))
            return new UploadAttachmentResponse { Success = false, ErrorMessage = "Invalid file node ID." };

        try
        {
            // Download file content from the Files module via gRPC streaming
            var downloadRequest = new DownloadFileRequest
            {
                NodeId = request.FileNodeId,
                UserId = request.UserId,
                VersionNumber = 0
            };

            await using var ms = new MemoryStream();
            string? fileName = null;
            string? contentType = null;

            using var downloadCall = _filesClient.Value.DownloadFile(downloadRequest, cancellationToken: context.CancellationToken);
            await foreach (var chunk in downloadCall.ResponseStream.ReadAllAsync(context.CancellationToken))
            {
                ms.Write(chunk.ChunkData.Span);
            }

            if (ms.Length == 0)
                return new UploadAttachmentResponse { Success = false, ErrorMessage = "File is empty or not found." };

            // Also get the file node metadata for filename/content type
            try
            {
                var nodeResponse = await _filesClient.Value.GetNodeAsync(
                    new GetNodeRequest { NodeId = request.FileNodeId, UserId = request.UserId },
                    cancellationToken: context.CancellationToken);
                if (nodeResponse.Found && nodeResponse.Node is not null)
                {
                    fileName = nodeResponse.Node.Name;
                    contentType = nodeResponse.Node.MimeType;
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("AttachFromFilesModule: file node {Id} not found via GetNode", fileNodeId);
                return new UploadAttachmentResponse { Success = false, ErrorMessage = "File node not found." };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AttachFromFilesModule: GetNode failed, proceeding without metadata");
            }

            ms.Position = 0;
            var result = await _attachmentStorage.StoreAsync(
                ms,
                fileName ?? "file.bin",
                contentType ?? "application/octet-stream",
                context.CancellationToken);

            return new UploadAttachmentResponse
            {
                Success = true,
                StorageKey = result.StorageKey,
                FileName = fileName ?? "file.bin",
                ContentType = contentType ?? "application/octet-stream",
                Size = result.Size,
                ContentHash = result.ContentHash
            };
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "AttachFromFilesModule gRPC failed for file {FileNodeId}", request.FileNodeId);
            return new UploadAttachmentResponse { Success = false, ErrorMessage = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AttachFromFilesModule failed for file {FileNodeId}", request.FileNodeId);
            return new UploadAttachmentResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ─── Search Index ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task GetSearchableDocuments(
        global::DotNetCloud.Modules.Email.Host.Protos.GetSearchableDocumentsRequest request,
        Grpc.Core.IServerStreamWriter<global::DotNetCloud.Modules.Email.Host.Protos.SearchableDocument> responseStream,
        ServerCallContext context)
    {
        var threads = await _db.EmailThreads
            .AsNoTracking()
            .Where(t => t.MessageCount > 0)
            .ToListAsync(context.CancellationToken);

        foreach (var thread in threads)
        {
            var doc = MapToSearchableDocument(thread);
            await responseStream.WriteAsync(doc, context.CancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task<global::DotNetCloud.Modules.Email.Host.Protos.SearchableDocumentResponse> GetSearchableDocument(
        global::DotNetCloud.Modules.Email.Host.Protos.GetSearchableDocumentRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EntityId, out var id))
        {
            return new global::DotNetCloud.Modules.Email.Host.Protos.SearchableDocumentResponse { Found = false };
        }

        var thread = await _db.EmailThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.MessageCount > 0, context.CancellationToken);

        if (thread is null)
        {
            return new global::DotNetCloud.Modules.Email.Host.Protos.SearchableDocumentResponse { Found = false };
        }

        return new global::DotNetCloud.Modules.Email.Host.Protos.SearchableDocumentResponse
        {
            Found = true,
            Document = MapToSearchableDocument(thread)
        };
    }

    private static global::DotNetCloud.Modules.Email.Host.Protos.SearchableDocument MapToSearchableDocument(EmailThread thread)
    {
        var content = thread.Subject;
        if (!string.IsNullOrEmpty(thread.Snippet))
            content += " " + thread.Snippet;

        return new global::DotNetCloud.Modules.Email.Host.Protos.SearchableDocument
        {
            ModuleId = "email",
            EntityId = thread.Id.ToString(),
            EntityType = "EmailThread",
            Title = thread.Subject,
            Content = content.Trim(),
            Summary = thread.Snippet ?? string.Empty,
            OwnerId = Guid.Empty.ToString(), // Email threads tied to accounts; ownership via account association
            CreatedAt = thread.CreatedAt.ToString("O"),
            UpdatedAt = thread.UpdatedAt.ToString("O")
        };
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

    private static ThreadMessage ToThreadMessage(EmailThread t) => new()
    {
        Id = t.Id.ToString(),
        AccountId = t.AccountId.ToString(),
        Subject = t.Subject,
        Snippet = t.Snippet ?? string.Empty,
        MessageCount = t.MessageCount,
        IsUnread = t.MessageCount > 0,
        IsStarred = false,
        LastMessageAt = t.LastMessageAt?.ToString("O") ?? string.Empty,
        ParticipantEmails = { ExtractEmailAddresses(t.ParticipantsJson) }
    };

    private static EmailMessageItem ToEmailMessageItem(EmailMessage m) => new()
    {
        Id = m.Id.ToString(),
        ThreadId = m.ThreadId.ToString(),
        FromAddress = ExtractFirstEmailAddress(m.FromJson),
        Subject = m.Subject,
        Snippet = m.BodyPreview ?? string.Empty,
        IsRead = m.IsRead,
        HasAttachments = m.Attachments?.Count > 0,
        ReceivedAt = m.DateReceived?.ToString("O") ?? m.CreatedAt.ToString("O"),
        ToAddresses = { ExtractEmailAddresses(m.ToJson) }
    };

    private static string ExtractFirstEmailAddress(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]")
            return string.Empty;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var first = root[0];
                if (first.TryGetProperty("email", out var email))
                    return email.GetString() ?? string.Empty;
                if (first.TryGetProperty("address", out var addr))
                    return addr.GetString() ?? string.Empty;
            }
        }
        catch { /* best effort */ }
        return string.Empty;
    }

    private static List<string> ExtractEmailAddresses(string json)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(json) || json == "[]")
            return result;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.TryGetProperty("email", out var email))
                        result.Add(email.GetString() ?? string.Empty);
                    else if (item.TryGetProperty("address", out var addr))
                        result.Add(addr.GetString() ?? string.Empty);
                }
            }
        }
        catch { /* best effort */ }
        return result;
    }
}

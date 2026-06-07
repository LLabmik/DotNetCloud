using System.Security.Claims;
using DotNetCloud.Modules.Email.Host.Protos;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Email gRPC client used by the Core Server.
/// </summary>
public sealed class EmailGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "EmailGrpc";
    /// <summary>The gRPC address of the Email module.</summary>
    public string EmailModuleAddress { get; set; } = "http://localhost:5013";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IEmailApiClient"/>.
/// Calls the Email module's gRPC service for all account, send, rule, thread,
/// message body, and attachment operations (including streaming upload/download).
/// </summary>
public sealed class EmailGrpcApiClient : IEmailApiClient, IDisposable
{
    private readonly EmailGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<EmailGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<EmailService.EmailServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="EmailGrpcApiClient"/> class.</summary>
    public EmailGrpcApiClient(
        IOptions<EmailGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<EmailGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<EmailService.EmailServiceClient>(
            () => new EmailService.EmailServiceClient(_channel.Value));
    }

    private string GetUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) ? userId : Guid.Empty.ToString();
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.email");
        _logger.LogInformation("EmailGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    // ─── Accounts ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailAccountDto>> ListAccountsAsync(CancellationToken ct = default)
    {
        var request = new ListAccountsRequest { UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListAccountsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Accounts.Select(ToAccount).Where(a => a is not null).Select(a => a!).ToList();
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.ListAccountsAsync failed"); return []; }
    }

    /// <inheritdoc />
    public async Task<EmailAccountDto?> GetAccountAsync(Guid id, CancellationToken ct = default)
    {
        var request = new GetAccountRequest { AccountId = id.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetAccountAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToAccount(response.Account) : null;
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.GetAccountAsync failed"); return null; }
    }

    /// <inheritdoc />
    public async Task<EmailAccountDto?> CreateAccountAsync(CreateEmailAccountRequest req, CancellationToken ct = default)
    {
        var request = new CreateAccountRequest
        {
            UserId = GetUserId(),
            ProviderType = req.ProviderType.ToString(),
            DisplayName = req.DisplayName,
            EmailAddress = req.EmailAddress,
            CredentialsJson = req.CredentialsJson ?? string.Empty
        };
        try
        {
            var response = await _client.Value.CreateAccountAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToAccount(response.Account) : null;
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.CreateAccountAsync failed"); return null; }
    }

    /// <inheritdoc />
    public async Task<EmailAccountDto?> UpdateAccountAsync(Guid id, UpdateEmailAccountRequest req, CancellationToken ct = default)
    {
        var request = new UpdateAccountRequest
        {
            AccountId = id.ToString(),
            UserId = GetUserId(),
            DisplayName = req.DisplayName ?? string.Empty,
            IsEnabled = req.IsEnabled ?? true
        };
        try
        {
            var response = await _client.Value.UpdateAccountAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToAccount(response.Account) : null;
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.UpdateAccountAsync failed"); return null; }
    }

    /// <inheritdoc />
    public async Task DeleteAccountAsync(Guid id, CancellationToken ct = default)
    {
        var request = new DeleteAccountRequest { AccountId = id.ToString(), UserId = GetUserId() };
        try
        { await _client.Value.DeleteAccountAsync(request, DeadlineHeaders(ct)).ResponseAsync; }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.DeleteAccountAsync failed"); }
    }

    // ─── Mailboxes ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailMailboxDto>> ListMailboxesAsync(Guid accountId, CancellationToken ct = default)
    {
        var request = new ListMailboxesRequest { AccountId = accountId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListMailboxesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Mailboxes.Select(ToMailbox).Where(m => m is not null).Select(m => m!).ToList();
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.ListMailboxesAsync failed"); return []; }
    }

    // ─── Send ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task SendAsync(Guid accountId, EmailSendRequest req, CancellationToken ct = default)
    {
        var request = new SendEmailRequest
        {
            AccountId = accountId.ToString(),
            UserId = GetUserId(),
            Subject = req.Subject ?? string.Empty,
            BodyHtml = req.BodyHtml ?? string.Empty,
            BodyText = req.BodyPlainText ?? string.Empty
        };
        request.To.AddRange(req.To?.Select(a => a.Email) ?? []);
        request.Cc.AddRange(req.Cc?.Select(a => a.Email) ?? []);
        request.Bcc.AddRange(req.Bcc?.Select(a => a.Email) ?? []);
        request.AttachmentStorageKeys.AddRange(req.Attachments?.Select(a => a.StorageKey) ?? []);
        try
        { await _client.Value.SendEmailAsync(request, DeadlineHeaders(ct)).ResponseAsync; }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.SendAsync failed"); }
    }

    // ─── Rules ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailRuleDto>> ListRulesAsync(Guid? accountId = null, CancellationToken ct = default)
    {
        var request = new ListRulesRequest
        {
            UserId = GetUserId(),
            AccountId = accountId?.ToString() ?? string.Empty
        };
        try
        {
            var response = await _client.Value.ListRulesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Rules.Select(ToRule).Where(r => r is not null).Select(r => r!).ToList();
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.ListRulesAsync failed"); return []; }
    }

    /// <inheritdoc />
    public async Task<EmailRuleDto?> GetRuleAsync(Guid id, CancellationToken ct = default)
    {
        var request = new GetRuleRequest { RuleId = id.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetRuleAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToRule(response.Rule) : null;
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.GetRuleAsync failed"); return null; }
    }

    /// <inheritdoc />
    public async Task<EmailRuleDto?> CreateRuleAsync(CreateEmailRuleRequest req, CancellationToken ct = default)
    {
        var request = new CreateRuleRequest
        {
            UserId = GetUserId(),
            Name = req.Name,
            AccountId = req.AccountId?.ToString() ?? string.Empty,
            IsEnabled = req.IsEnabled,
            Priority = req.Priority,
            StopProcessing = req.StopProcessing
        };
        try
        {
            var response = await _client.Value.CreateRuleAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToRule(response.Rule) : null;
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.CreateRuleAsync failed"); return null; }
    }

    /// <inheritdoc />
    public async Task<EmailRuleDto?> UpdateRuleAsync(Guid id, UpdateEmailRuleRequest req, CancellationToken ct = default)
    {
        var request = new UpdateRuleRequest
        {
            RuleId = id.ToString(),
            UserId = GetUserId(),
            Name = req.Name ?? string.Empty,
            IsEnabled = req.IsEnabled ?? true,
            Priority = req.Priority ?? 0,
            StopProcessing = req.StopProcessing ?? false
        };
        try
        {
            var response = await _client.Value.UpdateRuleAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? ToRule(response.Rule) : null;
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.UpdateRuleAsync failed"); return null; }
    }

    /// <inheritdoc />
    public async Task DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        var request = new DeleteRuleRequest { RuleId = id.ToString(), UserId = GetUserId() };
        try
        { await _client.Value.DeleteRuleAsync(request, DeadlineHeaders(ct)).ResponseAsync; }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.DeleteRuleAsync failed"); }
    }

    /// <inheritdoc />
    public async Task<int> RunRulesAsync(Guid? accountId = null, Guid? mailboxId = null, CancellationToken ct = default)
    {
        var request = new RunRulesRequest
        {
            UserId = GetUserId(),
            AccountId = accountId?.ToString() ?? string.Empty,
            MailboxId = mailboxId?.ToString() ?? string.Empty
        };
        try
        {
            var response = await _client.Value.RunRulesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? response.MatchedCount : 0;
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.RunRulesAsync failed"); return 0; }
    }

    // ─── Sync ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task TriggerSyncAsync(Guid accountId, CancellationToken ct = default)
    {
        var request = new TriggerSyncRequest { AccountId = accountId.ToString(), UserId = GetUserId() };
        try
        { await _client.Value.TriggerSyncAsync(request, DeadlineHeaders(ct)).ResponseAsync; }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.TriggerSyncAsync failed"); }
    }

    // ─── Gmail OAuth ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<bool> CheckGmailOAuthConfiguredAsync(CancellationToken ct = default)
    {
        // OAuth configuration check is a UI concern, not a gRPC operation
        return Task.FromResult(false);
    }

    // ─── Threads & Messages (gRPC) ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailThreadDto>> ListThreadsAsync(Guid accountId, Guid mailboxId, CancellationToken ct = default)
    {
        var request = new ListThreadsRequest
        {
            AccountId = accountId.ToString(),
            MailboxId = mailboxId.ToString(),
            UserId = GetUserId(),
            Skip = 0,
            Take = 100
        };
        try
        {
            var response = await _client.Value.ListThreadsAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Threads.Select(ToThread).Where(t => t is not null).Select(t => t!).ToList();
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.ListThreadsAsync failed"); return []; }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailMessageDto>> ListThreadMessagesAsync(Guid threadId, CancellationToken ct = default)
    {
        var request = new ListThreadMessagesRequest { ThreadId = threadId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.ListThreadMessagesAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return !response.Success ? [] : response.Messages.Select(ToMessage).Where(m => m is not null).Select(m => m!).ToList();
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.ListThreadMessagesAsync failed"); return []; }
    }

    // ─── Messages ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string?> GetMessageBodyAsync(Guid messageId, CancellationToken ct = default)
    {
        var request = new GetMessageBodyRequest { MessageId = messageId.ToString(), UserId = GetUserId() };
        try
        {
            var response = await _client.Value.GetMessageBodyAsync(request, DeadlineHeaders(ct)).ResponseAsync;
            return response.Success ? (string.IsNullOrEmpty(response.BodyHtml) ? null : response.BodyHtml) : null;
        }
        catch (RpcException ex) { _logger.LogError(ex, "EmailGrpcApiClient.GetMessageBodyAsync failed"); return null; }
    }

    // ─── Attachments (gRPC streaming) ───────────────────────────────────────

    /// <inheritdoc />
    public async Task<(Stream Stream, string FileName, string ContentType)> DownloadAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var request = new DownloadAttachmentRequest
        {
            AttachmentId = attachmentId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            using var call = _client.Value.DownloadAttachment(request, DeadlineHeaders(ct));
            var ms = new MemoryStream();
            string fileName = string.Empty;
            string contentType = "application/octet-stream";

            await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
            {
                ms.Write(chunk.Data.Span);
                if (!string.IsNullOrEmpty(chunk.FileName))
                    fileName = chunk.FileName;
                if (!string.IsNullOrEmpty(chunk.ContentType))
                    contentType = chunk.ContentType;
            }

            ms.Position = 0;
            return (ms, fileName, contentType);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "EmailGrpcApiClient.DownloadAttachmentAsync failed for {AttachmentId}", attachmentId);
            return (Stream.Null, string.Empty, string.Empty);
        }
    }

    /// <inheritdoc />
    public async Task<UploadAttachmentResult> UploadAttachmentAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        try
        {
            using var call = _client.Value.UploadAttachment(DeadlineHeaders(ct));

            // Read content in chunks and stream to gRPC
            var buffer = new byte[64 * 1024]; // 64 KB chunks
            int bytesRead;
            bool firstChunk = true;

            while ((bytesRead = await content.ReadAsync(buffer, ct)) > 0)
            {
                var chunk = new UploadAttachmentChunk
                {
                    Data = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead),
                    FileName = firstChunk ? (fileName ?? string.Empty) : string.Empty,
                    ContentType = firstChunk ? (contentType ?? "application/octet-stream") : string.Empty
                };
                await call.RequestStream.WriteAsync(chunk, ct);
                firstChunk = false;
            }

            await call.RequestStream.CompleteAsync();
            var response = await call.ResponseAsync;

            return response.Success
                ? new UploadAttachmentResult
                {
                    StorageKey = response.StorageKey,
                    FileName = response.FileName,
                    ContentType = response.ContentType,
                    Size = response.Size,
                    ContentHash = response.ContentHash
                }
                : new UploadAttachmentResult
                {
                    StorageKey = string.Empty,
                    FileName = fileName ?? string.Empty,
                    ContentType = contentType ?? string.Empty,
                    Size = 0,
                    ContentHash = string.Empty
                };
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "EmailGrpcApiClient.UploadAttachmentAsync failed");
            return new UploadAttachmentResult
            {
                StorageKey = string.Empty,
                FileName = fileName,
                ContentType = contentType,
                Size = 0,
                ContentHash = string.Empty
            };
        }
    }

    /// <inheritdoc />
    public async Task DetachAttachmentAsync(Guid attachmentId, Guid? targetFolderId = null, CancellationToken ct = default)
    {
        var request = new DetachAttachmentRequest
        {
            AttachmentId = attachmentId.ToString(),
            UserId = GetUserId(),
            TargetFolderId = targetFolderId?.ToString() ?? string.Empty
        };
        try
        {
            await _client.Value.DetachAttachmentAsync(request, DeadlineHeaders(ct));
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "EmailGrpcApiClient.DetachAttachmentAsync failed for {AttachmentId}", attachmentId);
        }
    }

    /// <inheritdoc />
    public async Task<UploadAttachmentResult> AttachFromFilesModuleAsync(Guid fileNodeId, CancellationToken ct = default)
    {
        var request = new AttachFromFilesRequest
        {
            FileNodeId = fileNodeId.ToString(),
            UserId = GetUserId()
        };
        try
        {
            var response = await _client.Value.AttachFromFilesModuleAsync(request, DeadlineHeaders(ct));
            return response.Success
                ? new UploadAttachmentResult
                {
                    StorageKey = response.StorageKey,
                    FileName = response.FileName,
                    ContentType = response.ContentType,
                    Size = response.Size,
                    ContentHash = response.ContentHash
                }
                : new UploadAttachmentResult
                {
                    StorageKey = string.Empty,
                    FileName = string.Empty,
                    ContentType = string.Empty,
                    Size = 0,
                    ContentHash = string.Empty
                };
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "EmailGrpcApiClient.AttachFromFilesModuleAsync failed for {FileNodeId}", fileNodeId);
            return new UploadAttachmentResult
            {
                StorageKey = string.Empty,
                FileName = string.Empty,
                ContentType = string.Empty,
                Size = 0,
                ContentHash = string.Empty
            };
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private Metadata DeadlineHeaders(CancellationToken ct)
    {
        var headers = new Metadata();
        if (_options.Timeout > TimeSpan.Zero)
            headers.Add("deadline", DateTime.UtcNow.Add(_options.Timeout).ToString("O"));
        return headers;
    }

    private static EmailAccountDto? ToAccount(AccountMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new EmailAccountDto
        {
            Id = Guid.Parse(m.Id),
            OwnerId = Guid.Parse(m.OwnerId),
            ProviderType = Enum.TryParse<EmailProviderType>(m.ProviderType, out var pt) ? pt : EmailProviderType.ImapSmtp,
            DisplayName = m.DisplayName,
            EmailAddress = m.EmailAddress,
            IsEnabled = m.IsEnabled,
            CreatedAt = DateTime.TryParse(m.CreatedAt, out var ca) ? ca : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(m.UpdatedAt, out var ua) ? ua : DateTime.MinValue
        };
    }

    private static EmailMailboxDto? ToMailbox(MailboxMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new EmailMailboxDto
        {
            Id = Guid.Parse(m.Id),
            AccountId = Guid.Parse(m.AccountId),
            DisplayName = m.Name,
            ProviderId = m.FullName,
            LastSyncedAt = DateTime.TryParse(m.LastSyncedAt, out var ls) ? ls : null
        };
    }

    private static EmailRuleDto? ToRule(RuleMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new EmailRuleDto
        {
            Id = Guid.Parse(m.Id),
            OwnerId = Guid.Parse(m.OwnerId),
            AccountId = string.IsNullOrEmpty(m.AccountId) ? null : Guid.Parse(m.AccountId),
            Name = m.Name,
            IsEnabled = m.IsEnabled,
            Priority = m.Priority,
            StopProcessing = m.StopProcessing,
            CreatedAt = DateTime.TryParse(m.CreatedAt, out var ca) ? ca : DateTime.MinValue,
            UpdatedAt = DateTime.TryParse(m.UpdatedAt, out var ua) ? ua : DateTime.MinValue
        };
    }

    private static EmailThreadDto? ToThread(ThreadMessage? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new EmailThreadDto
        {
            Id = Guid.Parse(m.Id),
            AccountId = Guid.Parse(m.AccountId),
            Subject = m.Subject,
            Snippet = m.Snippet,
            ParticipantsJson = System.Text.Json.JsonSerializer.Serialize(m.ParticipantEmails),
            MessageCount = m.MessageCount,
            LastMessageAt = DateTime.TryParse(m.LastMessageAt, out var lma) ? lma : null,
            CreatedAt = DateTime.MinValue,
            UpdatedAt = DateTime.MinValue
        };
    }

    private static EmailMessageDto? ToMessage(EmailMessageItem? m)
    {
        if (m is null || string.IsNullOrEmpty(m.Id))
            return null;
        return new EmailMessageDto
        {
            Id = Guid.Parse(m.Id),
            ThreadId = Guid.Parse(m.ThreadId),
            Subject = m.Subject,
            BodyPreview = m.Snippet,
            IsRead = m.IsRead,
            DateReceived = DateTime.TryParse(m.ReceivedAt, out var dr) ? dr : null
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_channel.IsValueCreated)
            {
                try
                { _channel.Value.Dispose(); }
                catch { /* best effort */ }
            }
        }
    }
}

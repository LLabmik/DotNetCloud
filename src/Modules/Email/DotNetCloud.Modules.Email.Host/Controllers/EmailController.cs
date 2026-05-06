using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Email.Host.Controllers;

/// <summary>
/// REST API controller for Email module endpoints.
/// </summary>
[Route("api/v1/email")]
public class EmailController : EmailControllerBase
{
    private readonly IEmailAccountService _accountService;
    private readonly IEmailRuleService _ruleService;
    private readonly IEmailSendService _sendService;
    private readonly IEmailSyncService _syncService;
    private readonly ISearchFtsClient? _searchFtsClient;
    private readonly IAttachmentStorage _attachmentStorage;
    private readonly IEventBus _eventBus;
    private readonly EmailDbContext _db;
    private readonly ILogger<EmailController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailController"/> class.
    /// </summary>
    public EmailController(
        IEmailAccountService accountService,
        IEmailRuleService ruleService,
        IEmailSendService sendService,
        IEmailSyncService syncService,
        ISearchFtsClient? searchFtsClient,
        IAttachmentStorage attachmentStorage,
        IEventBus eventBus,
        EmailDbContext db,
        ILogger<EmailController> logger)
    {
        _accountService = accountService;
        _ruleService = ruleService;
        _sendService = sendService;
        _syncService = syncService;
        _searchFtsClient = searchFtsClient;
        _attachmentStorage = attachmentStorage;
        _eventBus = eventBus;
        _db = db;
        _logger = logger;
    }

    // ── Accounts ───────────────────────────────────────────

    /// <summary>Lists email accounts for the authenticated user.</summary>
    [HttpGet("accounts")]
    public async Task<IActionResult> ListAccounts()
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var accounts = await _accountService.ListAsync(caller);
            return Ok(Envelope(accounts));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets an email account by ID.</summary>
    [HttpGet("accounts/{id:guid}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var account = await _accountService.GetAsync(id, caller);
            if (account is null) return NotFound(ErrorEnvelope(ErrorCodes.EmailAccountNotFound, "Email account not found."));
            return Ok(Envelope(account));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Creates a new email account.</summary>
    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateEmailAccountRequest request)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var account = await _accountService.CreateAsync(request, caller);
            return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, Envelope(account));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates an email account.</summary>
    [HttpPatch("accounts/{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateEmailAccountRequest request)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var account = await _accountService.UpdateAsync(id, request, caller);
            return Ok(Envelope(account));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.EmailAccountNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    /// <summary>Deletes an email account.</summary>
    [HttpDelete("accounts/{id:guid}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            await _accountService.DeleteAsync(id, caller);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.EmailAccountNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    // ── Send ───────────────────────────────────────────────

    /// <summary>Sends an email from the specified account.</summary>
    [HttpPost("accounts/{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, [FromBody] EmailSendRequest request)
    {
        _logger.LogInformation("EmailController.Send called: accountId={Id}, To={To}, Subject={Subj}",
            id, request.To?.Count, request.Subject);
        try
        {
            var caller = GetAuthenticatedCaller();
            _logger.LogInformation("EmailController.Send: caller authenticated, userId={UserId}", caller.UserId);
            await _sendService.SendAsync(id, request, caller);
            _logger.LogInformation("EmailController.Send: succeeded");
            return Ok(Envelope(new { sent = true }));
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("EmailController.Send: ValidationException - {Code}: {Msg}", ex.ErrorCode, ex.Message);
            return ex.ErrorCode switch
            {
                ErrorCodes.EmailAccountNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailController.Send: unhandled exception");
            throw;
        }
    }

    // ── Sync ───────────────────────────────────────────────

    /// <summary>Triggers a manual sync for an account.</summary>
    [HttpPost("accounts/{id:guid}/sync")]
    public async Task<IActionResult> SyncAccount(Guid id)
    {
        try
        {
            await _syncService.SyncAccountAsync(id);
            return Ok(Envelope(new { syncing = true }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ── Mailboxes ──────────────────────────────────────────

    /// <summary>Lists mailboxes for an email account.</summary>
    [HttpGet("accounts/{id:guid}/mailboxes")]
    public async Task<IActionResult> ListMailboxes(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var mailboxes = await _accountService.ListMailboxesAsync(id, caller);
            return Ok(Envelope(mailboxes));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.EmailAccountNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    // ── Threads ────────────────────────────────────────────

    /// <summary>Lists threads for a mailbox.</summary>
    [HttpGet("accounts/{id:guid}/mailboxes/{mailboxId:guid}/threads")]
    public async Task<IActionResult> ListThreads(Guid id, Guid mailboxId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();

            // Verify account ownership
            var account = await _db.EmailAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == caller.UserId);

            if (account is null)
                return NotFound(ErrorEnvelope(ErrorCodes.EmailAccountNotFound, "Email account not found."));

            // Find threads that have at least one message in this mailbox
            var threadIdsInMailbox = await _db.EmailMessages
                .AsNoTracking()
                .Where(m => m.MailboxId == mailboxId && m.AccountId == id)
                .Select(m => m.ThreadId)
                .Distinct()
                .ToListAsync();

            var threads = await _db.EmailThreads
                .AsNoTracking()
                .Where(t => threadIdsInMailbox.Contains(t.Id))
                .OrderByDescending(t => t.LastMessageAt)
                .Take(100)
                .ToListAsync();

            return Ok(Envelope(threads));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Lists messages in a thread.</summary>
    [HttpGet("threads/{threadId:guid}/messages")]
    public async Task<IActionResult> ListThreadMessages(Guid threadId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();

            var thread = await _db.EmailThreads
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == threadId);

            if (thread is null)
                return NotFound(ErrorEnvelope("thread_not_found", "Thread not found."));

            var account = await _db.EmailAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == thread.AccountId && a.OwnerId == caller.UserId);

            if (account is null)
                return NotFound(ErrorEnvelope(ErrorCodes.EmailAccountNotFound, "Email account not found."));

            var messages = await _db.EmailMessages
                .AsNoTracking()
                .Include(m => m.Attachments)
                .Where(m => m.ThreadId == threadId)
                .OrderBy(m => m.DateReceived)
                .ToListAsync();

            return Ok(Envelope(messages));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets the full HTML body of a message.</summary>
    [HttpGet("messages/{messageId:guid}/body")]
    public async Task<IActionResult> GetMessageBody(Guid messageId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();

            var message = await _db.EmailMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message is null)
                return NotFound(ErrorEnvelope("message_not_found", "Message not found."));

            var account = await _db.EmailAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == message.AccountId && a.OwnerId == caller.UserId);

            if (account is null)
                return NotFound(ErrorEnvelope(ErrorCodes.EmailAccountNotFound, "Email account not found."));

            return Ok(Envelope(new { bodyHtml = message.BodyHtml ?? "" }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ── Rules ──────────────────────────────────────────────

    /// <summary>Lists email rules for the authenticated user.</summary>
    [HttpGet("rules")]
    public async Task<IActionResult> ListRules([FromQuery] Guid? accountId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var rules = await _ruleService.ListAsync(caller, accountId);
            return Ok(Envelope(rules));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets an email rule by ID.</summary>
    [HttpGet("rules/{id:guid}")]
    public async Task<IActionResult> GetRule(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var rule = await _ruleService.GetAsync(id, caller);
            if (rule is null) return NotFound(ErrorEnvelope(ErrorCodes.EmailRuleNotFound, "Email rule not found."));
            return Ok(Envelope(rule));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Creates a new email rule.</summary>
    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateEmailRuleRequest request)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var rule = await _ruleService.CreateAsync(request, caller);
            return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, Envelope(rule));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates an email rule.</summary>
    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateEmailRuleRequest request)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var rule = await _ruleService.UpdateAsync(id, request, caller);
            return Ok(Envelope(rule));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.EmailRuleNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    /// <summary>Deletes an email rule.</summary>
    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            await _ruleService.DeleteAsync(id, caller);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode switch
            {
                ErrorCodes.EmailRuleNotFound => NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message)),
                _ => BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message))
            };
        }
    }

    /// <summary>Manually runs all enabled rules.</summary>
    [HttpPost("rules/run")]
    public async Task<IActionResult> RunRules([FromQuery] Guid? accountId, [FromQuery] Guid? mailboxId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            var count = await _ruleService.RunRulesAsync(caller, accountId, mailboxId);
            return Ok(Envelope(new { executed = count }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ── Attachments ─────────────────────────────────────────

    /// <summary>Downloads an attachment by ID.</summary>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, [FromQuery] bool inline = false)
    {
        try
        {
            var caller = GetAuthenticatedCaller();

            var attachment = await _db.EmailAttachments
                .AsNoTracking()
                .Include(a => a.Message)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment is null)
                return NotFound(ErrorEnvelope("attachment_not_found", "Attachment not found."));

            // Verify caller ownership via the parent account
            var account = await _db.EmailAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attachment.Message!.AccountId && a.OwnerId == caller.UserId);

            if (account is null)
                return Forbid();

            if (string.IsNullOrWhiteSpace(attachment.StorageKey))
                return NotFound(ErrorEnvelope("attachment_not_stored",
                    "This attachment was synced before content storage was implemented. Please re-sync the account."));

            var stream = await _attachmentStorage.OpenReadAsync(attachment.StorageKey, HttpContext.RequestAborted);
            if (stream is null)
                return NotFound(ErrorEnvelope("attachment_not_found", "Attachment content not found on disk."));

            var disposition = inline ? "inline" : "attachment";
            Response.Headers.Append("Content-Disposition", $"{disposition}; filename=\"{attachment.FileName}\"");

            return File(stream, attachment.ContentType, attachment.FileName);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Uploads a temporary attachment for use in compose. Stores via IAttachmentStorage.</summary>
    [HttpPost("upload-attachment")]
    [RequestSizeLimit(26 * 1024 * 1024)] // 26 MB (slightly above 25 MB limit to allow header overhead)
    [RequestFormLimits(MultipartBodyLengthLimit = 26 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(IFormFile file)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(ErrorEnvelope("no_file", "No file provided."));

            // Enforce 25 MB max file size
            const long maxSize = 25 * 1024 * 1024; // 25 MB
            if (file.Length > maxSize)
            {
                return StatusCode(413, new
                {
                    success = false,
                    error = new
                    {
                        code = "file_too_large",
                        message = "Files over 25 MB can be shared via the Files module."
                    }
                });
            }

            await using var stream = file.OpenReadStream();
            var result = await _attachmentStorage.StoreAsync(stream, file.FileName, file.ContentType, HttpContext.RequestAborted);

            return Ok(Envelope(new
            {
                storageKey = result.StorageKey,
                fileName = file.FileName,
                contentType = file.ContentType,
                size = result.Size,
                contentHash = result.ContentHash
            }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Detaches an attachment to the Files module. Publishes EmailAttachmentDetachedEvent.</summary>
    [HttpPost("attachments/{attachmentId:guid}/detach")]
    public async Task<IActionResult> DetachAttachment(Guid attachmentId, [FromQuery] Guid? targetFolderId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();

            var attachment = await _db.EmailAttachments
                .AsNoTracking()
                .Include(a => a.Message)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment is null)
                return NotFound(ErrorEnvelope("attachment_not_found", "Attachment not found."));

            // Verify caller ownership
            var account = await _db.EmailAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attachment.Message!.AccountId && a.OwnerId == caller.UserId);

            if (account is null)
                return Forbid();

            if (string.IsNullOrWhiteSpace(attachment.StorageKey))
                return BadRequest(ErrorEnvelope("attachment_not_stored",
                    "This attachment was synced before content storage was implemented."));

            // Publish event for Files module handler
            await _eventBus.PublishAsync(new EmailAttachmentDetachedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                AttachmentId = attachmentId,
                StorageKey = attachment.StorageKey,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                Size = attachment.Size,
                OwnerId = caller.UserId,
                TargetFolderId = targetFolderId
            }, caller, HttpContext.RequestAborted);

            return Ok(Envelope(new { detached = true }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}

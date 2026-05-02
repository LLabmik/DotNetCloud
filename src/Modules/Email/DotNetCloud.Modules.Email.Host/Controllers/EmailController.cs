using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Email.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.AspNetCore.Mvc;

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
        ILogger<EmailController> logger)
    {
        _accountService = accountService;
        _ruleService = ruleService;
        _sendService = sendService;
        _syncService = syncService;
        _searchFtsClient = searchFtsClient;
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
        try
        {
            var caller = GetAuthenticatedCaller();
            await _sendService.SendAsync(id, request, caller);
            return Ok(Envelope(new { sent = true }));
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
}

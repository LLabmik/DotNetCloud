using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Contacts.Host.Controllers;

/// <summary>
/// REST API controller for contact CRUD, search, groups, sharing, and vCard import/export.
/// </summary>
[Route("api/v1/contacts")]
public class ContactsController : ContactsControllerBase
{
    private readonly IContactService _contactService;
    private readonly IContactGroupService _groupService;
    private readonly IContactShareService _shareService;
    private readonly IVCardService _vcardService;
    private readonly ILogger<ContactsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsController"/> class.
    /// </summary>
    public ContactsController(
        IContactService contactService,
        IContactGroupService groupService,
        IContactShareService shareService,
        IVCardService vcardService,
        ILogger<ContactsController> logger)
    {
        _contactService = contactService;
        _groupService = groupService;
        _shareService = shareService;
        _vcardService = vcardService;
        _logger = logger;
    }

    // ─── Contact CRUD ─────────────────────────────────────────────────────

    /// <summary>Lists contacts for the authenticated user with optional search.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var contacts = await _contactService.ListContactsAsync(caller, search, skip, take);
        return Ok(Envelope(contacts));
    }

    /// <summary>Gets a contact by ID.</summary>
    [HttpGet("{contactId:guid}")]
    public async Task<IActionResult> GetAsync(Guid contactId)
    {
        var caller = GetAuthenticatedCaller();
        var contact = await _contactService.GetContactAsync(contactId, caller);
        return contact is null
            ? NotFound(ErrorEnvelope(ErrorCodes.ContactNotFound, "Contact not found."))
            : Ok(Envelope(contact));
    }

    /// <summary>Creates a new contact.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateContactDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var contact = await _contactService.CreateContactAsync(dto, caller);
            return Created($"/api/v1/contacts/{contact.Id}", Envelope(contact));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates an existing contact.</summary>
    [HttpPut("{contactId:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid contactId, [FromBody] UpdateContactDto dto)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var contact = await _contactService.UpdateContactAsync(contactId, dto, caller);
            return Ok(Envelope(contact));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.ContactNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Soft-deletes a contact.</summary>
    [HttpDelete("{contactId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid contactId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _contactService.DeleteContactAsync(contactId, caller);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Groups ───────────────────────────────────────────────────────────

    /// <summary>Lists contact groups for the authenticated user.</summary>
    [HttpGet("groups")]
    public async Task<IActionResult> ListGroupsAsync()
    {
        var caller = GetAuthenticatedCaller();
        var groups = await _groupService.ListGroupsAsync(caller);
        return Ok(Envelope(groups));
    }

    /// <summary>Gets a group by ID.</summary>
    [HttpGet("groups/{groupId:guid}")]
    public async Task<IActionResult> GetGroupAsync(Guid groupId)
    {
        var caller = GetAuthenticatedCaller();
        var group = await _groupService.GetGroupAsync(groupId, caller);
        return group is null
            ? NotFound(ErrorEnvelope(ErrorCodes.ContactGroupNotFound, "Group not found."))
            : Ok(Envelope(group));
    }

    /// <summary>Creates a new contact group.</summary>
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroupAsync([FromBody] CreateGroupRequest request)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var group = await _groupService.CreateGroupAsync(request.Name, caller);
            return Created($"/api/v1/contacts/groups/{group.Id}", Envelope(group));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Renames a contact group.</summary>
    [HttpPut("groups/{groupId:guid}")]
    public async Task<IActionResult> RenameGroupAsync(Guid groupId, [FromBody] CreateGroupRequest request)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var group = await _groupService.RenameGroupAsync(groupId, request.Name, caller);
            return Ok(Envelope(group));
        }
        catch (ValidationException ex)
        {
            return ex.ErrorCode == ErrorCodes.ContactGroupNotFound
                ? NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a contact group.</summary>
    [HttpDelete("groups/{groupId:guid}")]
    public async Task<IActionResult> DeleteGroupAsync(Guid groupId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _groupService.DeleteGroupAsync(groupId, caller);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Adds a contact to a group.</summary>
    [HttpPost("groups/{groupId:guid}/members/{contactId:guid}")]
    public async Task<IActionResult> AddToGroupAsync(Guid groupId, Guid contactId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            await _groupService.AddContactToGroupAsync(groupId, contactId, caller);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a contact from a group.</summary>
    [HttpDelete("groups/{groupId:guid}/members/{contactId:guid}")]
    public async Task<IActionResult> RemoveFromGroupAsync(Guid groupId, Guid contactId)
    {
        var caller = GetAuthenticatedCaller();
        await _groupService.RemoveContactFromGroupAsync(groupId, contactId, caller);
        return NoContent();
    }

    /// <summary>Lists contacts in a group.</summary>
    [HttpGet("groups/{groupId:guid}/members")]
    public async Task<IActionResult> ListGroupMembersAsync(Guid groupId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var members = await _groupService.ListGroupMembersAsync(groupId, caller);
            return Ok(Envelope(members));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Sharing ──────────────────────────────────────────────────────────

    /// <summary>Lists shares for a contact.</summary>
    [HttpGet("{contactId:guid}/shares")]
    public async Task<IActionResult> ListSharesAsync(Guid contactId)
    {
        var caller = GetAuthenticatedCaller();
        var shares = await _shareService.ListSharesAsync(contactId, caller);
        return Ok(Envelope(shares));
    }

    /// <summary>Shares a contact with a user or team.</summary>
    [HttpPost("{contactId:guid}/shares")]
    public async Task<IActionResult> ShareAsync(Guid contactId, [FromBody] ShareContactRequest request)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var share = await _shareService.ShareContactAsync(
                contactId, request.UserId, request.TeamId, request.Permission, caller);
            return Created($"/api/v1/contacts/{contactId}/shares", Envelope(share));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a contact share.</summary>
    [HttpDelete("shares/{shareId:guid}")]
    public async Task<IActionResult> RemoveShareAsync(Guid shareId)
    {
        var caller = GetAuthenticatedCaller();
        await _shareService.RemoveShareAsync(shareId, caller);
        return NoContent();
    }

    // ─── vCard Import/Export ──────────────────────────────────────────────

    /// <summary>Exports a single contact as vCard.</summary>
    [HttpGet("{contactId:guid}/vcard")]
    public async Task<IActionResult> ExportVCardAsync(Guid contactId)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var vcard = await _vcardService.ExportVCardAsync(contactId, caller);
            return Content(vcard, "text/vcard");
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Exports all contacts as vCard.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportAllAsync()
    {
        var caller = GetAuthenticatedCaller();
        var vcard = await _vcardService.ExportAllVCardsAsync(caller);
        return Content(vcard, "text/vcard");
    }

    /// <summary>Imports contacts from vCard data.</summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportVCardsAsync([FromBody] ImportVCardRequest request)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var ids = await _vcardService.ImportVCardsAsync(request.VCardData, caller);
            return Ok(Envelope(new { importedCount = ids.Count, contactIds = ids }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}

/// <summary>Request body for creating or renaming a group.</summary>
public sealed record CreateGroupRequest
{
    /// <summary>Group name.</summary>
    public required string Name { get; init; }
}

/// <summary>Request body for sharing a contact.</summary>
public sealed record ShareContactRequest
{
    /// <summary>User to share with (null if team-scoped).</summary>
    public Guid? UserId { get; init; }

    /// <summary>Team to share with (null if user-scoped).</summary>
    public Guid? TeamId { get; init; }

    /// <summary>Permission level.</summary>
    public ContactSharePermission Permission { get; init; } = ContactSharePermission.ReadOnly;
}

/// <summary>Request body for importing vCard data.</summary>
public sealed record ImportVCardRequest
{
    /// <summary>Raw vCard text (supports multiple vCards).</summary>
    public required string VCardData { get; init; }
}

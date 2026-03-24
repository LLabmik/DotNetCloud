using System.Text;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;

namespace DotNetCloud.Modules.Contacts.Host.Controllers;

/// <summary>
/// Custom HTTP method attribute for WebDAV methods (PROPFIND, REPORT, etc.).
/// Named to avoid conflict with ASP.NET Core's built-in HttpMethodAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class WebDavMethodAttribute : Attribute, IActionHttpMethodProvider, IRouteTemplateProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebDavMethodAttribute"/> class.
    /// </summary>
    public WebDavMethodAttribute(string method, string? template = null)
    {
        HttpMethods = [method];
        Template = template;
    }

    /// <inheritdoc />
    public IEnumerable<string> HttpMethods { get; }

    /// <inheritdoc />
    public string? Template { get; }

    /// <inheritdoc />
    public int? Order { get; }

    /// <inheritdoc />
    public string? Name { get; }
}

/// <summary>
/// CardDAV endpoints for contact interoperability with external clients
/// (Thunderbird, DAVx5, iOS/macOS Contacts, etc.).
/// Implements a subset of RFC 6352 (CardDAV) for address book access.
/// </summary>
[Route("carddav")]
[ApiController]
[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
public class CardDavController : ControllerBase
{
    private readonly IContactService _contactService;
    private readonly IVCardService _vcardService;
    private readonly ILogger<CardDavController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardDavController"/> class.
    /// </summary>
    public CardDavController(
        IContactService contactService,
        IVCardService vcardService,
        ILogger<CardDavController> logger)
    {
        _contactService = contactService;
        _vcardService = vcardService;
        _logger = logger;
    }

    /// <summary>
    /// Well-known CardDAV redirect (RFC 6764).
    /// Clients discover the address book URL by requesting /.well-known/carddav.
    /// </summary>
    [HttpGet("/.well-known/carddav")]
    [AllowAnonymous]
    public IActionResult WellKnown()
    {
        return Redirect("/carddav/");
    }

    /// <summary>
    /// DAV capability advertisement for CardDAV clients.
    /// </summary>
    [HttpOptions]
    [HttpOptions("{**path}")]
    public IActionResult DavOptions()
    {
        Response.Headers["DAV"] = "1, addressbook";
        Response.Headers.Allow = "OPTIONS, GET, PUT, DELETE, PROPFIND, REPORT";
        return Ok();
    }

    /// <summary>
    /// PROPFIND handler for address book discovery and resource properties.
    /// </summary>
    [WebDavMethod("PROPFIND", "")]
    [WebDavMethod("PROPFIND", "{userId:guid}")]
    [WebDavMethod("PROPFIND", "{userId:guid}/addressbook")]
    public async Task<IActionResult> PropFind(Guid? userId = null)
    {
        var caller = GetCaller();

        var contacts = await _contactService.ListContactsAsync(caller, take: 10000);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<D:multistatus xmlns:D=\"DAV:\" xmlns:C=\"urn:ietf:params:xml:ns:carddav\">");

        // Address book collection
        sb.AppendLine("  <D:response>");
        sb.Append("    <D:href>/carddav/").Append(caller.UserId).AppendLine("/addressbook/</D:href>");
        sb.AppendLine("    <D:propstat>");
        sb.AppendLine("      <D:prop>");
        sb.AppendLine("        <D:resourcetype><D:collection/><C:addressbook/></D:resourcetype>");
        sb.AppendLine("        <D:displayname>Contacts</D:displayname>");
        sb.AppendLine("      </D:prop>");
        sb.AppendLine("      <D:status>HTTP/1.1 200 OK</D:status>");
        sb.AppendLine("    </D:propstat>");
        sb.AppendLine("  </D:response>");

        // Individual contact entries
        foreach (var contact in contacts)
        {
            sb.AppendLine("  <D:response>");
            sb.Append("    <D:href>/carddav/")
              .Append(caller.UserId)
              .Append("/addressbook/")
              .Append(contact.Id)
              .AppendLine(".vcf</D:href>");
            sb.AppendLine("    <D:propstat>");
            sb.AppendLine("      <D:prop>");
            sb.Append("        <D:getetag>\"").Append(contact.ETag).AppendLine("\"</D:getetag>");
            sb.AppendLine("        <D:getcontenttype>text/vcard</D:getcontenttype>");
            sb.AppendLine("      </D:prop>");
            sb.AppendLine("      <D:status>HTTP/1.1 200 OK</D:status>");
            sb.AppendLine("    </D:propstat>");
            sb.AppendLine("  </D:response>");
        }

        sb.AppendLine("</D:multistatus>");

        return new ContentResult
        {
            Content = sb.ToString(),
            ContentType = "application/xml; charset=utf-8",
            StatusCode = 207
        };
    }

    /// <summary>
    /// Gets a single contact as vCard.
    /// </summary>
    [HttpGet("{userId:guid}/addressbook/{contactId:guid}.vcf")]
    public async Task<IActionResult> GetVCard(Guid userId, Guid contactId)
    {
        var caller = GetCaller();

        try
        {
            var vcard = await _vcardService.ExportVCardAsync(contactId, caller);

            var contact = await _contactService.GetContactAsync(contactId, caller);
            if (contact?.ETag is not null)
            {
                Response.Headers.ETag = $"\"{contact.ETag}\"";
            }

            return Content(vcard, "text/vcard");
        }
        catch (ValidationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Creates or updates a contact via vCard PUT.
    /// </summary>
    [HttpPut("{userId:guid}/addressbook/{contactId:guid}.vcf")]
    public async Task<IActionResult> PutVCard(Guid userId, Guid contactId)
    {
        var caller = GetCaller();

        using var reader = new StreamReader(Request.Body);
        var vCardText = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(vCardText))
        {
            return BadRequest("Empty vCard body.");
        }

        // Check if contact exists
        var existing = await _contactService.GetContactAsync(contactId, caller);

        if (existing is not null)
        {
            // Check ETag for conflict detection
            var ifMatch = Request.Headers.IfMatch.FirstOrDefault();
            if (ifMatch is not null && ifMatch != $"\"{existing.ETag}\"" && ifMatch != "*")
            {
                return StatusCode(412); // Precondition Failed
            }
        }

        // Import the vCard (creates new contact)
        var ids = await _vcardService.ImportVCardsAsync(vCardText, caller);

        if (ids.Count == 0)
        {
            return BadRequest("No valid vCard data found.");
        }

        var created = await _contactService.GetContactAsync(ids[0], caller);
        if (created?.ETag is not null)
        {
            Response.Headers.ETag = $"\"{created.ETag}\"";
        }

        return existing is null
            ? Created($"/carddav/{userId}/addressbook/{ids[0]}.vcf", null)
            : NoContent();
    }

    /// <summary>
    /// Deletes a contact.
    /// </summary>
    [HttpDelete("{userId:guid}/addressbook/{contactId:guid}.vcf")]
    public async Task<IActionResult> DeleteVCard(Guid userId, Guid contactId)
    {
        var caller = GetCaller();

        try
        {
            await _contactService.DeleteContactAsync(contactId, caller);
            return NoContent();
        }
        catch (ValidationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// REPORT handler for sync-collection reports (sync-token based change tracking).
    /// </summary>
    [WebDavMethod("REPORT", "{userId:guid}/addressbook")]
    public async Task<IActionResult> Report(Guid userId)
    {
        var caller = GetCaller();

        // Return all contacts (full sync for now; sync-token tracking will be refined)
        var contacts = await _contactService.ListContactsAsync(caller, take: 10000);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<D:multistatus xmlns:D=\"DAV:\" xmlns:C=\"urn:ietf:params:xml:ns:carddav\">");

        foreach (var contact in contacts)
        {
            sb.AppendLine("  <D:response>");
            sb.Append("    <D:href>/carddav/")
              .Append(caller.UserId)
              .Append("/addressbook/")
              .Append(contact.Id)
              .AppendLine(".vcf</D:href>");
            sb.AppendLine("    <D:propstat>");
            sb.AppendLine("      <D:prop>");
            sb.Append("        <D:getetag>\"").Append(contact.ETag).AppendLine("\"</D:getetag>");
            sb.AppendLine("      </D:prop>");
            sb.AppendLine("      <D:status>HTTP/1.1 200 OK</D:status>");
            sb.AppendLine("    </D:propstat>");
            sb.AppendLine("  </D:response>");
        }

        // Sync token (timestamp-based for now)
        sb.Append("  <D:sync-token>").Append(DateTime.UtcNow.Ticks).AppendLine("</D:sync-token>");
        sb.AppendLine("</D:multistatus>");

        return new ContentResult
        {
            Content = sb.ToString(),
            ContentType = "application/xml; charset=utf-8",
            StatusCode = 207
        };
    }

    private CallerContext GetCaller()
    {
        if (User?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authentication is required.");

        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claimValue, out var userId))
            throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CallerContext(userId, roles, CallerType.User);
    }
}

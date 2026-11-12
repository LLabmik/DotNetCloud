using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for the organizations the acting user belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Tracks is organization-scoped: a product belongs to an organization, so the UI cannot show
/// anything until it knows which organizations the user may see. A sandboxed module has no access to
/// <c>core.organizations</c>, so the list comes from the <see cref="IOrganizationDirectory"/>
/// capability — a gRPC call to Core.Server made with the calling module's identity.
/// </para>
/// <para>
/// The endpoint never takes a user id from the request. The subject is the asserting user, so a
/// caller can only ever list its own memberships.
/// </para>
/// </remarks>
[Route("api/v1")]
public class OrganizationsController : TracksControllerBase
{
    private readonly IOrganizationDirectory _organizationDirectory;
    private readonly ProductService _productService;
    private readonly ILogger<OrganizationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationsController"/> class.
    /// </summary>
    /// <param name="organizationDirectory">Capability client for organization membership.</param>
    /// <param name="productService">Product service, used to annotate product counts.</param>
    /// <param name="logger">Logger.</param>
    public OrganizationsController(
        IOrganizationDirectory organizationDirectory,
        ProductService productService,
        ILogger<OrganizationsController> logger)
    {
        _organizationDirectory = organizationDirectory;
        _productService = productService;
        _logger = logger;
    }

    /// <summary>Lists the organizations the authenticated user is an active member of.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("organizations")]
    public async Task<IActionResult> ListMyOrganizationsAsync(CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            var organizations = await _organizationDirectory.GetUserOrganizationsAsync(caller.UserId, ct);

            // Product counts are computed from this module's own data, so the picker can show how
            // much work sits behind each organization without a second round trip per tile.
            var items = new List<OrganizationSummary>(organizations.Count);
            foreach (var organization in organizations)
            {
                var products = await _productService.ListProductsByOrganizationAsync(organization.Id, ct);
                items.Add(new OrganizationSummary
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    Description = organization.Description,
                    MemberCount = organization.MemberCount,
                    TeamCount = organization.TeamCount,
                    CreatedAt = organization.CreatedAt,
                    ProductCount = products.Count,
                });
            }

            return Ok(Envelope(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list organizations for the authenticated caller");
            return StatusCode(500, ErrorEnvelope("INTERNAL_ERROR", "Failed to list organizations."));
        }
    }

    /// <summary>
    /// An organization entry returned to the module UI: the core's directory data plus the module's
    /// own product count.
    /// </summary>
    public sealed class OrganizationSummary
    {
        /// <summary>Gets or sets the organization id.</summary>
        public Guid Id { get; init; }

        /// <summary>Gets or sets the organization name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets or sets the organization description.</summary>
        public string? Description { get; init; }

        /// <summary>Gets or sets the number of active members in the organization.</summary>
        public int MemberCount { get; init; }

        /// <summary>Gets or sets the number of teams in the organization.</summary>
        public int TeamCount { get; init; }

        /// <summary>Gets or sets when the organization was created.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>Gets or sets how many products this module holds for the organization.</summary>
        public int ProductCount { get; init; }
    }
}

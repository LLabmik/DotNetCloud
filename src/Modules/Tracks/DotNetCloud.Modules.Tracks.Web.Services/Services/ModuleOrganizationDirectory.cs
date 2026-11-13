using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// Browser-side <see cref="IOrganizationDirectory"/>: the module UI resolves the acting user's
/// organizations through the module host, which holds the <c>IOrganizationDirectory</c> grant.
/// </summary>
/// <remarks>
/// <para>
/// Only the membership listing is real. Role-level queries return conservative defaults rather than
/// pretending to answer: an admin check that silently says "false" is safer than one that says
/// "true", and the module's own product roles are the ones the UI actually gates on.
/// </para>
/// </remarks>
public sealed class ModuleOrganizationDirectory : IOrganizationDirectory
{
    private readonly TracksApi _api;
    private readonly ILogger<ModuleOrganizationDirectory> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleOrganizationDirectory"/> class.</summary>
    /// <param name="api">The module API client.</param>
    /// <param name="logger">Logger.</param>
    public ModuleOrganizationDirectory(TracksApi api, ILogger<ModuleOrganizationDirectory> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The endpoint takes no user id: the host answers for the asserting user, so a caller can
            // only ever list its own memberships.
            return await _api.SendAsync<IReadOnlyList<OrganizationDto>>(
                HttpMethod.Get,
                "api/v1/organizations",
                cancellationToken: cancellationToken) ?? [];
        }
        catch (TracksApiException ex) when (ex.Code == ConsentRequiredException.ProfileConsentCode)
        {
            // The core refused because the user has not approved the profile scope. Returning an
            // empty list here would leave the product picker permanently empty with nothing to
            // click, so the caller is told to ask the shell for consent.
            _logger.LogInformation("The acting user has not consented to the profile scope yet");
            throw new ConsentRequiredException(ex.Message, "profile");
        }
        catch (Exception ex) when (ex is TracksApiException or HttpRequestException or NotSupportedException)
        {
            _logger.LogError(ex, "Could not load the acting user's organizations");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsOrganizationMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var organizations = await GetUserOrganizationsAsync(userId, cancellationToken);
        return organizations.Any(o => o.Id == organizationId);
    }

    /// <inheritdoc />
    public Task<OrganizationMemberInfo?> GetMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<OrganizationMemberInfo?>(null);

    /// <inheritdoc />
    public Task<bool> HasOrgRoleAsync(
        Guid organizationId,
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("HasOrgRoleAsync is not available in the module UI");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> HasManagerOrAboveRoleAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("HasManagerOrAboveRoleAsync is not available in the module UI");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> GetUserRoleIdsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
}

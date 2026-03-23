using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Resolves cross-module links into display-ready metadata by delegating
/// to the appropriate module directory capability.
/// </summary>
/// <remarks>
/// <para><b>Capability tier:</b> Public — automatically granted to all modules.</para>
/// <para>
/// This service unifies link resolution across Contacts, Calendar, Notes, and Files modules.
/// Modules call <see cref="ResolveAsync"/> with a set of link requests and receive
/// enriched display metadata (labels, URLs, resolution status).
/// </para>
/// </remarks>
public interface ICrossModuleLinkResolver : ICapabilityInterface
{
    /// <summary>
    /// Resolves a single cross-module link into display-ready metadata.
    /// </summary>
    /// <param name="linkType">The type of entity to resolve.</param>
    /// <param name="targetId">The target entity ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved link, or an unresolved placeholder if the target does not exist.</returns>
    Task<CrossModuleLinkDto> ResolveAsync(
        CrossModuleLinkType linkType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves multiple cross-module links in a single batch.
    /// </summary>
    /// <param name="requests">The link requests to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved links in the same order as the requests.</returns>
    Task<IReadOnlyList<CrossModuleLinkDto>> ResolveBatchAsync(
        IReadOnlyList<CrossModuleLinkRequest> requests,
        CancellationToken cancellationToken = default);
}

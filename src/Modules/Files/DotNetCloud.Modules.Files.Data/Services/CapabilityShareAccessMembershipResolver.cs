using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Files.Services;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Resolves Files share memberships through the Core team and group directory capabilities.
/// </summary>
internal sealed class CapabilityShareAccessMembershipResolver : IShareAccessMembershipResolver
{
    private readonly ITeamDirectory? _teamDirectory;
    private readonly IGroupDirectory? _groupDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilityShareAccessMembershipResolver"/> class.
    /// </summary>
    public CapabilityShareAccessMembershipResolver(
        ITeamDirectory? teamDirectory = null,
        IGroupDirectory? groupDirectory = null)
    {
        _teamDirectory = teamDirectory;
        _groupDirectory = groupDirectory;
    }

    /// <inheritdoc />
    public async Task<ShareAccessMembership> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var teamIds = _teamDirectory is null
            ? []
            : (await _teamDirectory.GetTeamsForUserAsync(userId, cancellationToken))
                .Select(team => team.Id)
                .ToArray();

        var groupIds = _groupDirectory is null
            ? []
            : (await _groupDirectory.GetGroupsForUserAsync(userId, cancellationToken))
                .Select(group => group.Id)
                .ToArray();

        return new ShareAccessMembership
        {
            TeamIds = teamIds,
            GroupIds = groupIds,
        };
    }
}
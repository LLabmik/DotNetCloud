namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Resolves the team and group memberships that should be considered when evaluating Files shares.
/// </summary>
public interface IShareAccessMembershipResolver
{
    /// <summary>
    /// Resolves the caller's current share-relevant memberships.
    /// </summary>
    Task<ShareAccessMembership> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Team and group memberships that can grant share-based access inside the Files module.
/// </summary>
public sealed record ShareAccessMembership
{
    /// <summary>
    /// Empty membership result.
    /// </summary>
    public static ShareAccessMembership Empty { get; } = new();

    /// <summary>
    /// Team IDs the caller belongs to.
    /// </summary>
    public IReadOnlyCollection<Guid> TeamIds { get; init; } = [];

    /// <summary>
    /// Group IDs the caller belongs to.
    /// </summary>
    public IReadOnlyCollection<Guid> GroupIds { get; init; } = [];
}
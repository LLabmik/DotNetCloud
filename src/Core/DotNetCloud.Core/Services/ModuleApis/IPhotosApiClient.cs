namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// One photo or album the Photos module reports as shared with a user.
/// </summary>
/// <param name="ShareId">The share row.</param>
/// <param name="PhotoId">The shared photo, when the share targets a photo.</param>
/// <param name="AlbumId">The shared album, when the share targets an album.</param>
/// <param name="Title">Display title resolved by the module (album title or file name).</param>
/// <param name="Permission">Permission level name (<c>ReadOnly</c>, <c>Download</c>, <c>Contribute</c>).</param>
/// <param name="CreatedAt">When the share was created.</param>
public sealed record PhotoSharedItem(
    Guid ShareId,
    Guid? PhotoId,
    Guid? AlbumId,
    string Title,
    string Permission,
    DateTime CreatedAt);

/// <summary>
/// gRPC API client interface for the Photos module.
/// </summary>
public interface IPhotosApiClient
{
    /// <summary>
    /// Lists the photos and albums shared with <paramref name="userId"/>, resolved by the module.
    /// </summary>
    /// <param name="userId">The recipient; the call carries a core-signed assertion for this user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shares, or an empty list when the module is unreachable.</returns>
    Task<IReadOnlyList<PhotoSharedItem>> GetSharedWithMeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

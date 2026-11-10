using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Core.SharedWithMe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.SharedWithMe;

/// <summary>
/// Adapts the Photos module's shared-with-me listing into an <see cref="ISharedWithMeProvider"/> so
/// Files can surface shared photos and albums as virtual deep-link entries under
/// <c>_DotNetCloud/SharedWithMe/Photos</c>.
/// </summary>
/// <remarks>
/// <para>
/// The module owns its own schema, so the core cannot read its share tables (Track C). The listing
/// therefore comes from the module's <c>GetSharedWithMe</c> RPC through
/// <see cref="IPhotosApiClient"/>: the core mints a per-user assertion, the module answers for that
/// user only, and each title is resolved on the module side.
/// </para>
/// <para>
/// A stopped or restarting module must not break the Files listing, so every failure degrades to an
/// empty list rather than throwing into the file browser.
/// </para>
/// </remarks>
public sealed class PhotosSharedWithMeProvider : ISharedWithMeProvider
{
    /// <summary>Stable module id for Photos.</summary>
    public const string PhotosModuleId = "photos";

    /// <summary>Material icon used for the Photos virtual folder.</summary>
    public const string PhotosIcon = "photo_library";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PhotosSharedWithMeProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosSharedWithMeProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a DI scope per call.</param>
    /// <param name="logger">Logger for graceful module-failure diagnostics.</param>
    public PhotosSharedWithMeProvider(IServiceScopeFactory scopeFactory, ILogger<PhotosSharedWithMeProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => PhotosModuleId;

    /// <inheritdoc />
    public string DisplayName => "Photos";

    /// <inheritdoc />
    public string IconName => PhotosIcon;

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await ListAsync(userId, cancellationToken)).Count;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IPhotosApiClient>();

        IReadOnlyList<PhotoSharedItem> shares;

        try
        {
            shares = await client.GetSharedWithMeAsync(userId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The client already maps RPC failures; this is the belt-and-braces path (a
            // misconfigured endpoint, for example).
            // The user id is an account identifier and is deliberately not logged.
            _logger.LogWarning(ex, "Photos shared-with-me lookup failed; returning empty");
            return [];
        }

        return MapItems(shares);
    }

    /// <summary>
    /// Maps the module's shares to shared-with-me items. A share whose entity could not be resolved
    /// (deleted, or access revoked) is skipped rather than surfaced with a blank title.
    /// </summary>
    /// <param name="shares">Shares reported by the module.</param>
    /// <returns>The virtual entries, one per share.</returns>
    internal static IReadOnlyList<SharedWithMeModuleItem> MapItems(IReadOnlyList<PhotoSharedItem> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);

        var items = new List<SharedWithMeModuleItem>(shares.Count);

        foreach (var share in shares)
        {
            if (string.IsNullOrWhiteSpace(share.Title))
            {
                continue;
            }

            if (share.AlbumId is { } albumId)
            {
                items.Add(new SharedWithMeModuleItem(
                    ModuleId: PhotosModuleId,
                    DisplayName: "Photos",
                    EntityId: albumId,
                    EntityType: "Album",
                    Title: share.Title,
                    Subtitle: $"Album · {share.Permission}",
                    DeepLink: $"/modules/dotnetcloud.photos?album={albumId}",
                    IconName: "photo_album",
                    UpdatedAt: share.CreatedAt));
            }
            else if (share.PhotoId is { } photoId)
            {
                items.Add(new SharedWithMeModuleItem(
                    ModuleId: PhotosModuleId,
                    DisplayName: "Photos",
                    EntityId: photoId,
                    EntityType: "Photo",
                    Title: share.Title,
                    Subtitle: $"Photo · {share.Permission}",
                    DeepLink: $"/modules/dotnetcloud.photos?photoId={photoId}",
                    IconName: PhotosIcon,
                    UpdatedAt: share.CreatedAt));
            }
        }

        return items;
    }
}

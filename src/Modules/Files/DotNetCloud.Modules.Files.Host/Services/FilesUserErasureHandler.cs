using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Files.Data.Events;

namespace DotNetCloud.Modules.Files.Host.Services;

/// <summary>
/// Erases a deleted user's Files data on behalf of the core's erasure orchestrator.
/// </summary>
/// <remarks>
/// The work itself lives in <see cref="UserDeletedEventSubscriber"/>, which already knows every
/// table that carries per-user rows (file nodes and versions, quotas, default content, sync
/// devices, sync counters). Reusing it keeps one definition of "everything Files holds for a user"
/// instead of a second, drifting one.
/// </remarks>
internal sealed class FilesUserErasureHandler : IModuleUserErasureHandler
{
    private readonly UserDeletedEventSubscriber _subscriber;

    /// <summary>Initializes a new instance of the <see cref="FilesUserErasureHandler"/> class.</summary>
    /// <param name="subscriber">The existing per-user cleanup implementation.</param>
    public FilesUserErasureHandler(UserDeletedEventSubscriber subscriber)
    {
        _subscriber = subscriber;
    }

    /// <inheritdoc />
    public async Task<ModuleUserErasureResult> EraseAsync(
        ModuleUserErasureContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await _subscriber.HandleAsync(
            new UserDeletedEvent
            {
                EventId = context.RequestId == Guid.Empty ? Guid.CreateVersion7() : context.RequestId,
                CreatedAt = DateTime.UtcNow,
                UserId = context.UserId,
                DeletedAt = DateTime.UtcNow,
            },
            cancellationToken);

        return ModuleUserErasureResult.Success();
    }
}

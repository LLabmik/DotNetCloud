namespace DotNetCloud.Core.SharedWithMe;

/// <summary>
/// Identifies a module that can contribute items to the Files "Shared With Me" virtual tree.
/// The "files" module itself is never represented here — Files lists its native file-share
/// items itself. This only describes *other* modules (Notes, Photos, Contacts, Calendar) that
/// expose a shared-with-me provider.
/// </summary>
/// <param name="ModuleId">Stable module identifier, e.g. <c>"notes"</c>.</param>
/// <param name="DisplayName">Human-readable module name shown as the virtual folder, e.g. <c>"Notes"</c>.</param>
/// <param name="IconName">Material icon ligature name used when rendering the module folder (e.g. <c>"edit_note"</c>).</param>
public sealed record SharedWithMeModule(string ModuleId, string DisplayName, string IconName);

/// <summary>
/// A single item shared with a user, surfaced as a virtual read-only entry inside the module's
/// virtual folder under Files' <c>SharedWithMe</c> tree. Ephemeral — computed at list time and
/// never persisted to <c>MountedNodeEntry</c>.
/// </summary>
/// <param name="ModuleId">Module that owns the item (e.g. <c>"notes"</c>).</param>
/// <param name="DisplayName">Module display name.</param>
/// <param name="EntityId">Identifier of the underlying module entity (e.g. the note id).</param>
/// <param name="EntityType">Type discriminator for the entity, e.g. <c>"Note"</c>.</param>
/// <param name="Title">Display title for the virtual entry (e.g. the note title).</param>
/// <param name="Subtitle">Optional secondary line (e.g. owner/sharer name). May be <see langword="null"/>.</param>
/// <param name="DeepLink">Application route that opens the item in the owning module (e.g. <c>"/apps/notes?noteId=..."</c>).</param>
/// <param name="IconName">Material icon ligature for the item. May be <see langword="null"/> to fall back to the module folder icon.</param>
/// <param name="UpdatedAt">Optional last-modified time used for the virtual entry's sort/display date.</param>
public sealed record SharedWithMeModuleItem(
    string ModuleId,
    string DisplayName,
    Guid EntityId,
    string EntityType,
    string Title,
    string? Subtitle,
    string DeepLink,
    string? IconName = null,
    DateTime? UpdatedAt = null);

/// <summary>
/// Implemented by a module adapter that can report the module's items shared with a given user.
/// Instances are registered in the core process (Core.Server) where module UI services are
/// available in-process; the Files module never references these implementations directly.
/// </summary>
public interface ISharedWithMeProvider
{
    /// <summary>Stable module identifier, e.g. <c>"notes"</c>.</summary>
    string ModuleId { get; }

    /// <summary>Human-readable module name shown as the virtual folder name.</summary>
    string DisplayName { get; }

    /// <summary>Material icon ligature used for the module's virtual folder and items.</summary>
    string IconName { get; }

    /// <summary>Returns the number of the module's items shared with <paramref name="userId"/>.</summary>
    /// <param name="userId">The recipient user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the module's items shared with <paramref name="userId"/>.</summary>
    /// <param name="userId">The recipient user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Slim, module-neutral facade consumed by the Files module to discover other modules' shared-with-me
/// contributions. Implemented and registered in Core.Server (aggregation is a core concern). The Files
/// module degrades gracefully when no registry is registered (process-isolated Files.Host and unit tests):
/// in that case the legacy file-share-only "Shared With Me" listing is preserved.
/// </summary>
public interface ISharedWithMeModuleRegistry
{
    /// <summary>
    /// Descriptors for modules (other than Files) that have a shared-with-me provider registered.
    /// </summary>
    IReadOnlyList<SharedWithMeModule> Modules { get; }

    /// <summary>Counts the items module <paramref name="moduleId"/> has shared with <paramref name="userId"/>.</summary>
    /// <param name="moduleId">Module id (e.g. <c>"notes"</c>).</param>
    /// <param name="userId">The recipient user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> CountAsync(string moduleId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Lists the items module <paramref name="moduleId"/> has shared with <paramref name="userId"/>.</summary>
    /// <param name="moduleId">Module id (e.g. <c>"notes"</c>).</param>
    /// <param name="userId">The recipient user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(string moduleId, Guid userId, CancellationToken cancellationToken = default);
}

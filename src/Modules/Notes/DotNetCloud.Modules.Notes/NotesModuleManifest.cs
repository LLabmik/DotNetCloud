using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Notes;

/// <summary>
/// Module manifest for the Notes module.
/// Declares capabilities, published events, and subscribed events.
/// </summary>
public sealed class NotesModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.notes";

    /// <inheritdoc />
    public string Name => "Notes";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.ICurrentUserContext),
        nameof(Core.Capabilities.IAuditLogger),
        nameof(Core.Capabilities.ICrossModuleLinkResolver),
        nameof(Core.Capabilities.IContactDirectory),
        nameof(Core.Capabilities.ICalendarDirectory)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(NoteCreatedEvent),
        nameof(NoteUpdatedEvent),
        nameof(NoteDeletedEvent),
        nameof(ResourceSharedEvent),
        nameof(UserMentionedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        nameof(ContactCreatedEvent),
        nameof(CalendarEventCreatedEvent)
    };
}

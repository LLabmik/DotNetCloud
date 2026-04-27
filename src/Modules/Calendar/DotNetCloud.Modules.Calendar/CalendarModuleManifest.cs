using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Calendar;

/// <summary>
/// Manifest for the Calendar module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class CalendarModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.calendar";

    /// <inheritdoc />
    public string Name => "Calendar";

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
        nameof(Core.Capabilities.IOrganizationDirectory)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(CalendarEventCreatedEvent),
        nameof(CalendarEventUpdatedEvent),
        nameof(CalendarEventDeletedEvent),
        nameof(CalendarEventRsvpEvent),
        nameof(CalendarReminderTriggeredEvent),
        nameof(ReminderTriggeredEvent),
        nameof(ResourceSharedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        nameof(ContactCreatedEvent),
        nameof(ContactDeletedEvent)
    };
}

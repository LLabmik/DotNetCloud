using CommunityToolkit.Mvvm.Messaging.Messages;

namespace DotNetCloud.Client.Android.Messages;

/// <summary>
/// Message sent when a calendar event is created/updated/deleted via push notification,
/// signaling the calendar page to refresh its data.
/// </summary>
public sealed class CalendarEventChangedMessage : ValueChangedMessage<bool>
{
    /// <summary>Initializes a new <see cref="CalendarEventChangedMessage"/>.</summary>
    public CalendarEventChangedMessage()
        : base(true)
    {
    }
}

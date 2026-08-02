using CommunityToolkit.Mvvm.Messaging.Messages;

namespace DotNetCloud.Client.Android.Messages;

/// <summary>
/// Message sent when the sum of unread counts across all chat channels changes.
/// Consumers (e.g. the app shell) use it to surface a total-unread indicator.
/// </summary>
public sealed class TotalUnreadCountChangedMessage : ValueChangedMessage<int>
{
    /// <summary>Initializes a new <see cref="TotalUnreadCountChangedMessage"/>.</summary>
    public TotalUnreadCountChangedMessage(int totalUnread)
        : base(totalUnread)
    {
    }
}

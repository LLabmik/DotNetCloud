using CommunityToolkit.Mvvm.Messaging.Messages;

namespace DotNetCloud.Client.Android.Messages;

/// <summary>
/// Message sent when a note is created or updated via the note editor,
/// signaling the notes list page to refresh.
/// </summary>
public sealed class NoteSavedMessage : ValueChangedMessage<bool>
{
    /// <summary>Initializes a new <see cref="NoteSavedMessage"/>.</summary>
    public NoteSavedMessage(bool isNew)
        : base(isNew)
    {
    }
}

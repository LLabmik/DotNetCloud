using CommunityToolkit.Mvvm.Messaging;
using DotNetCloud.Client.Android.Messages;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.Views;

namespace DotNetCloud.Client.Android;

/// <summary>Application shell — defines top-level navigation structure and routes.</summary>
public partial class AppShell : Shell
{
    private static ShellContent? _musicTab;
    private static ShellContent? _chatTab;

    /// <summary>Initializes a new <see cref="AppShell"/> and registers detail routes.</summary>
    public AppShell()
    {
        InitializeComponent();
        _musicTab = MusicTab;
        _chatTab = ChatTab;

        // Register routes for detail pages not expressed in the ShellContent hierarchy
        Routing.RegisterRoute("MessageList", typeof(MessageListPage));
        Routing.RegisterRoute("ChannelDetails", typeof(ChannelDetailsPage));
        Routing.RegisterRoute("EventDetail", typeof(EventDetailPage));
        Routing.RegisterRoute("EventEdit", typeof(EventEditPage));
        Routing.RegisterRoute("ImageViewer", typeof(ImageViewerPage));
        Routing.RegisterRoute("ChatImageViewer", typeof(Views.ChatImageViewerPage));
        Routing.RegisterRoute("NoteEdit", typeof(NoteEditPage));

        // Reflect total unread chat count on the Chat tab label (e.g. "Chat" → "Chat 3").
        WeakReferenceMessenger.Default.Register<TotalUnreadCountChangedMessage>(this, static (_, m) =>
        {
            if (_chatTab is not null)
                _chatTab.Title = m.Value > 0 ? $"Chat {m.Value}" : "Chat";
        });
    }

    /// <summary>
    /// Called by <see cref="App"/> after module availability is determined.
    /// Directly sets the Music tab's visibility.
    /// </summary>
    public static void SetMusicTabVisible(bool visible)
    {
        if (_musicTab is not null)
            _musicTab.IsVisible = visible;
    }

    /// <summary>
    /// Re-reads <see cref="ModuleAvailabilityState"/> and updates all tab visibilities
    /// accordingly. Called after a full module rescan.
    /// </summary>
    public static void RefreshAllTabs()
    {
        SetMusicTabVisible(ModuleAvailabilityState.IsMusicModuleAvailable);
    }
}


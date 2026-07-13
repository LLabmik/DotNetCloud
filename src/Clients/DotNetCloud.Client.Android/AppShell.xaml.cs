using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.Views;

namespace DotNetCloud.Client.Android;

/// <summary>Application shell — defines top-level navigation structure and routes.</summary>
public partial class AppShell : Shell
{
    private static ShellContent? _musicTab;

    /// <summary>Initializes a new <see cref="AppShell"/> and registers detail routes.</summary>
    public AppShell()
    {
        InitializeComponent();
        _musicTab = MusicTab;

        // Register routes for detail pages not expressed in the ShellContent hierarchy
        Routing.RegisterRoute("MessageList", typeof(MessageListPage));
        Routing.RegisterRoute("ChannelDetails", typeof(ChannelDetailsPage));
        Routing.RegisterRoute("EventDetail", typeof(EventDetailPage));
        Routing.RegisterRoute("EventEdit", typeof(EventEditPage));
        Routing.RegisterRoute("ImageViewer", typeof(ImageViewerPage));
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


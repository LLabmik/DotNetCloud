using CommunityToolkit.Mvvm.Messaging;
using DotNetCloud.Client.Android.Messages;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.Views;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android;

/// <summary>Application shell — defines top-level navigation structure and routes.</summary>
public partial class AppShell : Shell
{
    private static FlyoutItem? _musicTab;
    private static FlyoutItem? _chatTab;
    private static FlyoutItem? _aiTab;

    /// <summary>Initializes a new <see cref="AppShell"/> and registers detail routes.</summary>
    public AppShell()
    {
        InitializeComponent();
        _musicTab = MusicTab;
        _chatTab = ChatTab;
        _aiTab = AiTab;

        // Register routes for detail pages not expressed in the ShellContent hierarchy
        Routing.RegisterRoute("MessageList", typeof(MessageListPage));
        Routing.RegisterRoute("ChannelDetails", typeof(ChannelDetailsPage));
        Routing.RegisterRoute("EventDetail", typeof(EventDetailPage));
        Routing.RegisterRoute("EventEdit", typeof(EventEditPage));
        Routing.RegisterRoute("ImageViewer", typeof(ImageViewerPage));
        Routing.RegisterRoute("ChatImageViewer", typeof(Views.ChatImageViewerPage));
        Routing.RegisterRoute("NoteEdit", typeof(NoteEditPage));
        Routing.RegisterRoute("DmUserPicker", typeof(DmUserPickerPage));

        // Reflect total unread chat count on the Chat flyout entry (e.g. "Chat" → "Chat 3").
        WeakReferenceMessenger.Default.Register<TotalUnreadCountChangedMessage>(this, static (_, m) =>
        {
            if (_chatTab is not null)
                _chatTab.Title = m.Value > 0 ? $"Chat {m.Value}" : "Chat";
        });
    }

    /// <summary>
    /// Called by <see cref="App"/> after module availability is determined.
    /// Directly sets the Music flyout entry's visibility.
    /// </summary>
    public static void SetMusicTabVisible(bool visible)
    {
        if (_musicTab is null)
            return;

        Shell.SetFlyoutItemIsVisible(_musicTab, visible);
        _musicTab.IsVisible = visible;
    }

    /// <summary>Shows/hides the AI flyout entry.</summary>
    public static void SetAiTabVisible(bool visible)
    {
        if (_aiTab is null)
            return;

        Shell.SetFlyoutItemIsVisible(_aiTab, visible);
        _aiTab.IsVisible = visible;
    }

    /// <summary>
    /// Re-reads <see cref="ModuleAvailabilityState"/> and updates all flyout entry
    /// visibilities accordingly. Called after a full module rescan.
    /// </summary>
    public static void RefreshAllTabs()
    {
        SetMusicTabVisible(ModuleAvailabilityState.IsMusicModuleAvailable);
        SetAiTabVisible(ModuleAvailabilityState.IsAiModuleAvailable);
    }

    /// <summary>
    /// Handles the drawer's "Rescan Modules" action: closes the drawer, shows transient
    /// toast feedback, and triggers the full module rescan so newly available Music/AI
    /// entries appear on the next drawer open.
    /// </summary>
    private async void OnRescanModulesClicked(object? sender, TappedEventArgs e)
    {
        FlyoutIsPresented = false;
        ShowToast("Rescanning modules…");
        await App.TriggerModuleRescanAsync();
        ShowToast("Modules rescanned");
    }

    /// <summary>Shows a short native Android toast. Best-effort — never throws.</summary>
    private static void ShowToast(string message)
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity is null)
                return;

            global::Android.Widget.Toast.MakeText(activity, message, global::Android.Widget.ToastLength.Short)?.Show();
        }
        catch
        {
            // Best effort — a toast failure must never crash the app.
        }
    }
}


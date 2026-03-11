using DotNetCloud.Client.Android.Views;

namespace DotNetCloud.Client.Android;

/// <summary>Application shell — defines top-level navigation structure and routes.</summary>
public partial class AppShell : Shell
{
    /// <summary>Initializes a new <see cref="AppShell"/> and registers detail routes.</summary>
    public AppShell()
    {
        InitializeComponent();

        // Register routes for detail pages not expressed in the ShellContent hierarchy
        Routing.RegisterRoute("MessageList", typeof(MessageListPage));
    }
}


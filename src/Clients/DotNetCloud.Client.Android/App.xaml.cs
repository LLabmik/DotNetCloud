using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android;

/// <summary>MAUI application entry point.</summary>
public partial class App : Application
{
    private readonly IServerConnectionStore _serverStore;

    /// <summary>Initializes a new <see cref="App"/>.</summary>
    public App(IServerConnectionStore serverStore)
    {
        InitializeComponent();
        _serverStore = serverStore;
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    /// <inheritdoc />
    protected override async void OnStart()
    {
        base.OnStart();
        await NavigateToStartPageAsync().ConfigureAwait(false);
    }

    private async Task NavigateToStartPageAsync()
    {
        // Navigate to the channel list if a server connection is already active,
        // otherwise drop the user on the login screen.
        if (_serverStore.GetActive() is not null)
            await Shell.Current.GoToAsync("//Main/ChannelList").ConfigureAwait(false);
        else
            await Shell.Current.GoToAsync("//Login").ConfigureAwait(false);
    }
}
